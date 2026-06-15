using Microsoft.Extensions.Configuration;

namespace Pokemon2.Server.Llm;

public sealed class LlmOptions
{
    public LlmEndpointOptions Reply { get; init; } = new();
    public LlmEndpointOptions Choices { get; init; } = new();
    public int RateLimitPerMinute { get; init; } = 30;

    public bool IsConfigured(LlmOperation operation) => Resolve(operation).IsConfigured;

    public LlmEndpointOptions Resolve(LlmOperation operation)
    {
        return operation == LlmOperation.Reply ? Reply : Choices;
    }

    public static LlmOptions FromConfiguration(IConfiguration configuration)
    {
        var sharedApiKey = Clean(configuration["POKEMON2_LLM_API_KEY"]);
        var sharedApiUrl = Clean(configuration["POKEMON2_LLM_API_URL"]);
        var sharedModel = Clean(configuration["POKEMON2_LLM_MODEL"]);
        var sharedPromptCost = GetDecimal(configuration["POKEMON2_LLM_PROMPT_COST_PER_1K_USD"]);
        var sharedCompletionCost = GetDecimal(configuration["POKEMON2_LLM_COMPLETION_COST_PER_1K_USD"]);

        return new LlmOptions
        {
            Reply = LlmEndpointOptions.FromConfiguration(
                configuration,
                "REPLY",
                sharedApiKey,
                sharedApiUrl,
                sharedModel,
                120,
                sharedPromptCost,
                sharedCompletionCost),
            Choices = LlmEndpointOptions.FromConfiguration(
                configuration,
                "CHOICES",
                sharedApiKey,
                sharedApiUrl,
                sharedModel,
                180,
                sharedPromptCost,
                sharedCompletionCost),
            RateLimitPerMinute = Math.Max(1, GetInt(configuration["POKEMON2_LLM_RATE_LIMIT_PER_MINUTE"], 30))
        };
    }

    private static int GetInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static decimal GetDecimal(string? value)
    {
        return decimal.TryParse(value, out var parsed) ? parsed : 0m;
    }

    private static string Clean(string? value)
    {
        return value?.Trim() ?? "";
    }
}

public sealed class LlmEndpointOptions
{
    public string ApiKey { get; init; } = "";
    public string ApiUrl { get; init; } = "";
    public string Model { get; init; } = "";
    public int MaxOutputTokens { get; init; }
    public decimal PromptCostPer1KUsd { get; init; }
    public decimal CompletionCostPer1KUsd { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ApiUrl) &&
        !string.IsNullOrWhiteSpace(Model);

    public decimal EstimateCostUsd(int promptTokens, int completionTokens)
    {
        var promptCost = PromptCostPer1KUsd * promptTokens / 1000m;
        var completionCost = CompletionCostPer1KUsd * completionTokens / 1000m;
        return decimal.Round(promptCost + completionCost, 8, MidpointRounding.AwayFromZero);
    }

    public static LlmEndpointOptions FromConfiguration(
        IConfiguration configuration,
        string prefix,
        string sharedApiKey,
        string sharedApiUrl,
        string sharedModel,
        int defaultMaxOutputTokens,
        decimal sharedPromptCost,
        decimal sharedCompletionCost)
    {
        return new LlmEndpointOptions
        {
            ApiKey = Clean(configuration[$"POKEMON2_LLM_{prefix}_API_KEY"], sharedApiKey),
            ApiUrl = Clean(configuration[$"POKEMON2_LLM_{prefix}_API_URL"], sharedApiUrl),
            Model = Clean(configuration[$"POKEMON2_LLM_{prefix}_MODEL"], sharedModel),
            MaxOutputTokens = Math.Max(1, GetInt(configuration[$"POKEMON2_LLM_{prefix}_MAX_OUTPUT_TOKENS"], defaultMaxOutputTokens)),
            PromptCostPer1KUsd = GetDecimal(configuration[$"POKEMON2_LLM_{prefix}_PROMPT_COST_PER_1K_USD"], sharedPromptCost),
            CompletionCostPer1KUsd = GetDecimal(configuration[$"POKEMON2_LLM_{prefix}_COMPLETION_COST_PER_1K_USD"], sharedCompletionCost)
        };
    }

    private static int GetInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static decimal GetDecimal(string? value, decimal fallback)
    {
        return decimal.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string Clean(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
