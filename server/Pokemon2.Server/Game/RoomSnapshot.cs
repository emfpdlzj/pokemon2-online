namespace Pokemon2.Server.Game;

public sealed record RoomSnapshot(
    string RoomId,
    long ServerTick,
    long ServerTimeMs,
    IReadOnlyCollection<PlayerSnapshot> Players,
    IReadOnlyCollection<MonsterSnapshot> Monsters,
    IReadOnlyCollection<BattleSnapshot> Battles);

public sealed record PlayerSnapshot(
    string PlayerId,
    string Name,
    Position Position,
    Direction Facing);

public sealed record MonsterSnapshot(
    string MonsterId,
    string Name,
    Position Position,
    int Hp,
    int MaxHp,
    bool IsAlive);

public sealed record BattleSnapshot(
    string BattleId,
    string PlayerId,
    string MonsterId,
    int PlayerHp,
    int PlayerMp,
    int MonsterHp,
    bool Active);

public sealed record RoomSummary(
    string RoomId,
    string RoomName,
    string MapId,
    string MapName,
    int PlayerCount,
    int MaxPlayers,
    int MonsterCount,
    int ActiveBattleCount,
    long ServerTick,
    DateTimeOffset CreatedAt);
