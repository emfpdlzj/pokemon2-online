using System.Net.WebSockets;
using System.Text.Json;

namespace Pokemon2.Server.Game;

public abstract record RoomCommand
{
    public sealed record Join(
        string PlayerId,
        string PlayerName,
        WebSocket Socket,
        CancellationToken CancellationToken) : RoomCommand;

    public sealed record Leave(string PlayerId) : RoomCommand;

    public sealed record Move(
        string PlayerId,
        Direction Direction,
        long Sequence,
        CancellationToken CancellationToken) : RoomCommand;

    public sealed record Attack(
        string PlayerId,
        string? MonsterId,
        string? SkillId,
        long Sequence,
        CancellationToken CancellationToken) : RoomCommand;

    public sealed record Chat(string PlayerId, string Message) : RoomCommand;

    public sealed record BattleEvent(string PlayerId, JsonElement Payload) : RoomCommand;

    public sealed record Tick : RoomCommand;
}
