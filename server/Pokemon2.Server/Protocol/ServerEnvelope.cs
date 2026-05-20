namespace Pokemon2.Server.Protocol;

public sealed record ServerEnvelope(string Type, object Payload);
