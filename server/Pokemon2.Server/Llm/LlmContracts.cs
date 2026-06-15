namespace Pokemon2.Server.Llm;

public sealed record LlmReplyRequest(string? Character, string? Message);

public sealed record LlmReplyResponse(string Reply);

public sealed record LlmChoicesRequest(string? Message);

public sealed record LlmChoicesResponse(string[] Choices);
