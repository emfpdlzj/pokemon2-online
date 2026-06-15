namespace Pokemon2.Server.Llm;

public interface ILlmTextClient
{
    bool IsConfigured { get; }

    Task<string> GenerateTextAsync(string systemPrompt, string userMessage, int maxOutputTokens, CancellationToken cancellationToken);
}
