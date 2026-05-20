using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Pokemon2.Server.Game;
using Pokemon2.Server.Protocol;

namespace Pokemon2.Server.Networking;

public sealed class WebSocketGameEndpoint
{
    private readonly RoomManager _rooms;
    private readonly ILogger<WebSocketGameEndpoint> _logger;

    public WebSocketGameEndpoint(RoomManager rooms, ILogger<WebSocketGameEndpoint> logger)
    {
        _rooms = rooms;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context, string? roomId, string? playerName)
    {
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var playerId = Guid.NewGuid().ToString("N");
        var safeName = string.IsNullOrWhiteSpace(playerName) ? $"player-{playerId[..4]}" : playerName.Trim()[..Math.Min(playerName.Trim().Length, 16)];
        var room = _rooms.GetOrCreate(roomId);

        await room.EnqueueAsync(new RoomCommand.Join(playerId, safeName, socket, context.RequestAborted));

        try
        {
            await ReceiveLoopAsync(socket, room, playerId, context.RequestAborted);
        }
        finally
        {
            await room.EnqueueAsync(new RoomCommand.Leave(playerId));
        }
    }

    private async Task ReceiveLoopAsync(WebSocket socket, RoomActor room, string playerId, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (!result.EndOfMessage)
            {
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Fragmented messages are not supported.", cancellationToken);
                break;
            }

            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            if (!TryParseClientMessage(json, out var message))
            {
                _logger.LogWarning("Invalid client packet from {PlayerId}: {Packet}", playerId, json);
                continue;
            }

            switch (message.Type)
            {
                case "move" when Enum.TryParse<Direction>(message.Direction, true, out var direction):
                    await room.EnqueueAsync(new RoomCommand.Move(playerId, direction, message.Sequence, cancellationToken));
                    break;
                case "attack":
                    await room.EnqueueAsync(new RoomCommand.Attack(playerId, message.MonsterId, message.SkillId, message.Sequence, cancellationToken));
                    break;
                case "chat" when !string.IsNullOrWhiteSpace(message.Message):
                    await room.EnqueueAsync(new RoomCommand.Chat(playerId, message.Message.Trim()[..Math.Min(message.Message.Trim().Length, 80)]));
                    break;
                case "battle_event" when message.Payload.HasValue:
                    await room.EnqueueAsync(new RoomCommand.BattleEvent(playerId, message.Payload.Value));
                    break;
            }
        }
    }

    private static bool TryParseClientMessage(string json, out ClientMessage message)
    {
        try
        {
            message = JsonSerializer.Deserialize<ClientMessage>(json, JsonOptions.Default) ?? new ClientMessage();
            return !string.IsNullOrWhiteSpace(message.Type);
        }
        catch
        {
            message = new ClientMessage();
            return false;
        }
    }
}
