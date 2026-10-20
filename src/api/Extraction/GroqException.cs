namespace RecipeApp.Api.Extraction;

public enum GroqFailure
{
    Disabled,
    RateLimitMinute,
    RateLimitDay,
    Auth,
    Model,
    Validation,
    Timeout,
    Network,
    Unknown,
}

/// <summary>
/// A Groq failure whose message is already user-facing Danish (shown in the
/// import warnings and in the meal-plan banner). Never contains the API key.
/// </summary>
public class GroqException(string message, GroqFailure kind) : Exception(message)
{
    public GroqFailure Kind { get; } = kind;
}
