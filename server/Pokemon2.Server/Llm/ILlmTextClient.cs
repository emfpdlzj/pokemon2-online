namespace Pokemon2.Server.Llm;

public interface ILlmTextClient
{
    bool IsConfigured(LlmOperation operation);

    Task<LlmCompletionResult> GenerateTextAsync(LlmCompletionRequest request, CancellationToken cancellationToken);
}

public enum LlmOperation
{
    Reply,
    Choices
}

public sealed record LlmCompletionRequest(
    LlmOperation Operation,
    string SystemPrompt,
    string UserMessage);

public sealed record LlmCompletionUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    decimal EstimatedCostUsd);

public sealed record LlmCompletionResult(
    string Text,
    string Model,
    LlmCompletionUsage Usage);

public sealed class LlmRateLimitException : Exception
{
    public LlmRateLimitException(TimeSpan retryAfter)
        : base("LLM request rate limit exceeded.")
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }
}

public sealed class LlmInvalidResponseException : Exception
{
    public LlmInvalidResponseException(string message)
        : base(message)
    {
    }
}
