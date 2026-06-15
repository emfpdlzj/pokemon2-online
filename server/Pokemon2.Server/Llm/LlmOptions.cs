using Microsoft.Extensions.Configuration;

namespace Pokemon2.Server.Llm;

public sealed class LlmOptions
{
    public string ApiKey { get; init; } = "";
    public string ApiUrl { get; init; } = "";
    public string Model { get; init; } = "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ApiUrl) &&
        !string.IsNullOrWhiteSpace(Model);

    public static LlmOptions FromConfiguration(IConfiguration configuration)
    {
        return new LlmOptions
        {
            ApiKey = configuration["POKEMON2_LLM_API_KEY"]?.Trim() ?? "",
            ApiUrl = configuration["POKEMON2_LLM_API_URL"]?.Trim() ?? "",
            Model = configuration["POKEMON2_LLM_MODEL"]?.Trim() ?? ""
        };
    }
}
