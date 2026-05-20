namespace Pokemon2.Server.Game;

public sealed record RoomSnapshot(
    string RoomId,
    long ServerTick,
    long ServerTimeMs,
    IReadOnlyCollection<PlayerSnapshot> Players);

public sealed record PlayerSnapshot(
    string PlayerId,
    string Name,
    Position Position,
    Direction Facing);

public sealed record RoomSummary(
    string RoomId,
    string RoomName,
    string MapId,
    string MapName,
    int PlayerCount,
    int MaxPlayers,
    long ServerTick,
    DateTimeOffset CreatedAt);
