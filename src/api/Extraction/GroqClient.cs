using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RecipeApp.Api.Extraction;

/// <summary>
/// One shared entry point for every Groq chat-completions call. Owns the
/// retry ladder for parameter variants Groq rejects, the per-hour attempt cap
/// and the translation of provider errors into clear Danish messages —
/// especially rate limits, where the user needs to know it resets on its own.
/// </summary>
public class GroqClient(HttpClient http, IOptions<GroqOptions> options, ILogger<GroqClient> log)
{
    private static readonly Lock AttemptsLock = new();
    private static readonly Queue<DateTimeOffset> Attempts = new();

    // Groq tells us once which parameter variants it accepts for the model;
    // remember that for the rest of the process instead of retrying every call.
    private static bool _useReasoningEffort = true;
    private static bool _useCompletionTokens;
    private static bool _useJsonObject;

    private GroqOptions Options => options.Value;

    public bool Enabled => Options.Enabled;

    public string Model => Options.ModelValue;

    public async Task<string> CompleteJsonAsync(
        string systemPrompt, string userContent, JsonElement schema, CancellationToken ct)
    {
        if (!Enabled)
        {
            throw new GroqException(
                "AI er ikke konfigureret. Tilføj GROQ_API_KEY for at aktivere det.",
                GroqFailure.Disabled);
        }

        ReserveAttempt();

        var tokenParam = _useCompletionTokens ? "max_completion_tokens" : "max_tokens";
        var reasoning = _useReasoningEffort;
        var structured = !_useJsonObject;

        try
        {
            var response = await PostAsync(systemPrompt, userContent, schema, structured, tokenParam, reasoning, ct);

            // Each rejection teaches us a variant to avoid from now on.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (reasoning && await MentionsAsync(response, "reasoning_effort"))
                {
                    response.Dispose();
                    reasoning = false;
                    _useReasoningEffort = false;
                    log.LogInformation("Groq rejected reasoning_effort; learning to omit it");
                    response = await PostAsync(systemPrompt, userContent, schema, structured, tokenParam, reasoning, ct);
                    continue;
                }

                if (tokenParam == "max_tokens" && await MentionsAsync(response, "max_tokens"))
                {
                    response.Dispose();
                    tokenParam = "max_completion_tokens";
                    _useCompletionTokens = true;
                    log.LogInformation("Groq rejected max_tokens; learning {Param}", tokenParam);
                    response = await PostAsync(systemPrompt, userContent, schema, structured, tokenParam, reasoning, ct);
                    continue;
                }

                if (structured && await MentionsAsync(response, "response_format", "json_schema"))
                {
                    response.Dispose();
                    structured = false;
                    _useJsonObject = true;
                    log.LogInformation("Groq rejected json_schema; learning json_object");
                    response = await PostAsync(systemPrompt, userContent, schema, structured, tokenParam, reasoning, ct);
                    continue;
                }

                // Strict validation fails with an empty failed_generation when
                // the generation was truncated; json_object tolerates it better.
                if (structured && await MentionsAsync(response, "json_validate_failed", "Failed to validate JSON"))
                {
                    response.Dispose();
                    structured = false;
                    _useJsonObject = true;
                    log.LogWarning("Groq could not validate the strict schema; falling back to json_object");
                    response = await PostAsync(systemPrompt, userContent, schema, structured, tokenParam, reasoning, ct);
                    continue;
                }

                break;
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(ct);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throw RateLimited(response, body);
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new GroqException(
                        "AI-nøglen blev afvist af Groq – opdatér GROQ_API_KEY og deploy igen.",
                        GroqFailure.Auth);
                }

                if (response.StatusCode == HttpStatusCode.NotFound ||
                    body.Contains("model_not_found", StringComparison.OrdinalIgnoreCase) ||
                    body.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
                {
                    throw new GroqException(
                        $"AI-modellen \"{Model}\" er ikke tilgængelig på din Groq-konto – sæt variablen GROQ_MODEL til en model du har adgang til.",
                        GroqFailure.Model);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new GroqException(
                        response.StatusCode == HttpStatusCode.BadRequest &&
                        body.Contains("json", StringComparison.OrdinalIgnoreCase)
                            ? "AI kunne ikke levere gyldig JSON til opskriften – prøv igen eller udfyld manuelt."
                            : $"AI-tjenesten svarede {(int)response.StatusCode}: {Truncate(body, 240)}",
                        GroqFailure.Unknown);
                }

                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("usage", out var usage))
                {
                    log.LogInformation("Groq tokens: {Usage}", usage.ToString());
                }

                var content = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new GroqException(
                        "AI returnerede et tomt svar – prøv igen.",
                        GroqFailure.Validation);
                }

                return StripFences(content);
            }
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new GroqException($"AI-svaret tog for lang tid (>{Options.TimeoutSeconds}s) – prøv igen.", GroqFailure.Timeout);
        }
        catch (HttpRequestException ex)
        {
            log.LogWarning(ex, "Groq request failed");
            throw new GroqException(
                "Kunne ikke nå AI-tjenesten (netværk eller DNS) – prøv igen senere.",
                GroqFailure.Network);
        }
    }

    private async Task<HttpResponseMessage> PostAsync(
        string systemPrompt, string userContent, JsonElement schema,
        bool structured, string tokenParam, bool reasoning, CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = Model,
            ["temperature"] = 0,
            [tokenParam] = 2_500,
            ["messages"] = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent },
            },
            ["response_format"] = structured
                ? new
                {
                    type = "json_schema",
                    json_schema = new { name = "result", strict = true, schema },
                }
                : new { type = "json_object" },
        };

        // GPT-OSS spends completion budget on hidden reasoning; keep it low so
        // the JSON actually fits. Other models do not get the parameter.
        if (reasoning && Model.Contains("gpt-oss", StringComparison.OrdinalIgnoreCase))
        {
            payload["reasoning_effort"] = "low";
        }

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        // Await inside the using scope: returning the task would dispose the
        // request (and its StringContent) before it is sent.
        return await http.SendAsync(request, ct);
    }

    private static async Task<bool> MentionsAsync(HttpResponseMessage response, params string[] needles)
    {
        if ((int)response.StatusCode != 400)
        {
            return false;
        }
        var body = await response.Content.ReadAsStringAsync();
        return needles.Any(n => body.Contains(n, StringComparison.OrdinalIgnoreCase));
    }

    private static GroqException RateLimited(HttpResponseMessage response, string body)
    {
        var retry = response.Headers.RetryAfter;
        TimeSpan? wait = retry?.Delta;
        if (wait is null && retry?.Date is { } date)
        {
            wait = date - DateTimeOffset.UtcNow;
        }

        var perDay = body.Contains("per day", StringComparison.OrdinalIgnoreCase)
                     || body.Contains("daily", StringComparison.OrdinalIgnoreCase);
        var perMinute = body.Contains("per minute", StringComparison.OrdinalIgnoreCase)
                        || body.Contains("tokens per min", StringComparison.OrdinalIgnoreCase);

        if (!perDay && (perMinute || wait is { TotalSeconds: > 0 and <= 120 }))
        {
            var seconds = wait is { TotalSeconds: > 0 } w
                ? $" om ca. {Math.Max(1, (int)w.TotalSeconds)} sekunder"
                : " om et øjeblik";
            return new GroqException(
                $"AI-grænsen for dette minut er nået – prøv igen{seconds}.",
                GroqFailure.RateLimitMinute);
        }

        return new GroqException(
            "Den daglige gratis AI-kvote er brugt op. Den nulstilles automatisk ved midnat UTC – prøv igen i morgen.",
            GroqFailure.RateLimitDay);
    }

    private void ReserveAttempt()
    {
        lock (AttemptsLock)
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
            while (Attempts.Count > 0 && Attempts.Peek() < cutoff)
            {
                Attempts.Dequeue();
            }

            if (Attempts.Count >= Options.MaxAttemptsPerHour)
            {
                throw new GroqException(
                    $"AI er sat på pause efter {Options.MaxAttemptsPerHour} forsøg på en time – prøv igen senere.",
                    GroqFailure.LocalQuota);
            }

            Attempts.Enqueue(DateTimeOffset.UtcNow);
        }
    }

    private static string StripFences(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }
        var firstLine = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstLine >= 0 && lastFence > firstLine
            ? trimmed[(firstLine + 1)..lastFence].Trim()
            : trimmed;
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length] + "…";
}
