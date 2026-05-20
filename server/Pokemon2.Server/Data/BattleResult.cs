namespace Pokemon2.Server.Data;

public sealed class BattleResult
{
    public Guid Id { get; set; }
    public string RoomId { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string MonsterId { get; set; } = "";
    public string MonsterName { get; set; } = "";
    public bool Won { get; set; }
    public int ServerTick { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
