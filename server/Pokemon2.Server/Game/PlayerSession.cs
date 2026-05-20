using System.Net.WebSockets;
using Pokemon2.Server.Protocol;

namespace Pokemon2.Server.Game;

public sealed class PlayerSession
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public PlayerSession(string playerId, string name, WebSocket socket, Position position)
    {
        PlayerId = playerId;
        Name = name;
        Socket = socket;
        Position = position;
    }

    public string PlayerId { get; }
    public string Name { get; }
    public WebSocket Socket { get; }
    public Position Position { get; set; }
    public Direction Facing { get; set; } = Direction.Down;
    public long LastSequence { get; set; }
    public DateTimeOffset LastMoveAt { get; set; } = DateTimeOffset.MinValue;

    public PlayerSnapshot ToSnapshot()
    {
        return new PlayerSnapshot(PlayerId, Name, Position, Facing);
    }

    public async Task SendJsonAsync(object payload, CancellationToken cancellationToken)
    {
        if (Socket.State != WebSocketState.Open) return;

        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions.Default);
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await Socket.SendAsync(json, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
