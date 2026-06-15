namespace Pokemon2.Server.Llm;

public sealed record LlmReplyRequest(string? Character, string? Message);

public sealed record LlmReplyResponse(
    string Reply,
    string Source,
    string Status,
    string Character,
    string Model);

public sealed record LlmChoicesRequest(string? Message);

public sealed record LlmChoicesResponse(
    string[] Choices,
    string Source,
    string Status,
    string Model);
