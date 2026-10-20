namespace RecipeApp.Api.Extraction;

/// <summary>
/// Optional LLM fallback (Groq, OpenAI-compatible chat completions). Bound from
/// the "Groq" configuration section / Groq__* environment variables. With no
/// API key the extractor reports CanHandle=false and the chain behaves exactly
/// as before — the deterministic extractors remain the primary path.
/// </summary>
public class GroqOptions
{
    public string? ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    public string Model { get; set; } = "openai/gpt-oss-120b";

    public int TimeoutSeconds { get; set; } = 60;

    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>Trimmed key; secrets pasted into GitHub often carry a trailing newline.</summary>
    public string ApiKeyValue => (ApiKey ?? string.Empty).Trim();

    public string ModelValue => string.IsNullOrWhiteSpace(Model) ? "openai/gpt-oss-120b" : Model.Trim();
}
