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

    public bool IsConfigured => _options.IsConfigured;

    public async Task<string> GenerateTextAsync(string systemPrompt, string userMessage, int maxOutputTokens, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("LLM is not configured. Set POKEMON2_LLM_API_KEY, POKEMON2_LLM_API_URL, and POKEMON2_LLM_MODEL on the server.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = _options.Model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            },
            max_tokens = maxOutputTokens
        }), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("LLM provider returned {StatusCode}.", (int)response.StatusCode);
            throw new HttpRequestException($"LLM provider request failed with HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(payload);
        var text = ParseChoiceContent(document.RootElement);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("LLM provider response did not contain usable text.");
        }

        return text.Trim();
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
}
