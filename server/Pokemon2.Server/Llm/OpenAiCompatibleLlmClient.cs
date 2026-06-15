using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Pokemon2.Server.Llm;

public sealed class OpenAiCompatibleLlmClient : ILlmTextClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<OpenAiCompatibleLlmClient> _logger;

    public OpenAiCompatibleLlmClient(HttpClient httpClient, LlmOptions options, ILogger<OpenAiCompatibleLlmClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public bool IsConfigured(LlmOperation operation) => _options.IsConfigured(operation);

    public async Task<LlmCompletionResult> GenerateTextAsync(LlmCompletionRequest request, CancellationToken cancellationToken)
    {
        var endpoint = _options.Resolve(request.Operation);
        if (!endpoint.IsConfigured)
        {
            throw new InvalidOperationException($"LLM {request.Operation} endpoint is not configured.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint.ApiUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.ApiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = endpoint.Model,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserMessage }
            },
            max_tokens = endpoint.MaxOutputTokens
        }), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if ((int)response.StatusCode == StatusCodes.Status429TooManyRequests)
        {
            throw new LlmRateLimitException(TimeSpan.FromMinutes(1));
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("LLM provider returned {StatusCode} for {Operation}.", (int)response.StatusCode, request.Operation);
            throw new HttpRequestException($"LLM provider request failed with HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(payload);
        var text = ParseChoiceContent(document.RootElement);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new LlmInvalidResponseException("LLM provider response did not contain usable text.");
        }

        var usage = ParseUsage(document.RootElement, endpoint);
        var model = document.RootElement.TryGetProperty("model", out var modelElement)
            ? modelElement.GetString()?.Trim()
            : null;

        return new LlmCompletionResult(
            text.Trim(),
            string.IsNullOrWhiteSpace(model) ? endpoint.Model : model,
            usage);
    }

    private static string? ParseChoiceContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return null;
        }

        var first = choices[0];
        if (!first.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content))
        {
            return null;
        }

        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString(),
            JsonValueKind.Array => ParseContentParts(content),
            _ => null
        };
    }

    private static string ParseContentParts(JsonElement content)
    {
        var builder = new StringBuilder();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                builder.AppendLine(item.GetString());
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("type", out var type) &&
                string.Equals(type.GetString(), "text", StringComparison.OrdinalIgnoreCase) &&
                item.TryGetProperty("text", out var text))
            {
                builder.AppendLine(text.GetString());
            }
        }

        return builder.ToString().Trim();
    }

    private static LlmCompletionUsage ParseUsage(JsonElement root, LlmEndpointOptions endpoint)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return new LlmCompletionUsage(0, 0, 0, 0m);
        }

        var promptTokens = ReadUsageValue(usage, "prompt_tokens");
        var completionTokens = ReadUsageValue(usage, "completion_tokens");
        var totalTokens = ReadUsageValue(usage, "total_tokens");
        if (totalTokens == 0)
        {
            totalTokens = promptTokens + completionTokens;
        }

        return new LlmCompletionUsage(
            promptTokens,
            completionTokens,
            totalTokens,
            endpoint.EstimateCostUsd(promptTokens, completionTokens));
    }

    private static int ReadUsageValue(JsonElement usage, string propertyName)
    {
        return usage.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : 0;
    }
}
