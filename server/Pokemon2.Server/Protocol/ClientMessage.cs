namespace Pokemon2.Server.Protocol;

using System.Text.Json;

public sealed class ClientMessage
{
    public string? Type { get; set; }
    public string? Direction { get; set; }
    public long Sequence { get; set; }
    public string? Message { get; set; }
    public JsonElement? Payload { get; set; }
}
