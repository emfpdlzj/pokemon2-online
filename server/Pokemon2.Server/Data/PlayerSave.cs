namespace Pokemon2.Server.Data;

public sealed class PlayerSave
{
    public Guid Id { get; set; }
    public int SlotNumber { get; set; }
    public string Mode { get; set; } = "single";
    public string PlayerName { get; set; } = "주인공";
    public string CurrentMap { get; set; } = "hometown";
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public string? StarterId { get; set; }
    public string? StarterName { get; set; }
    public int? StarterLevel { get; set; }
    public int? StarterCurrentHp { get; set; }
    public long PlayTimeSeconds { get; set; }
    public string EventsJson { get; set; } = "{}";
    public string GameStateJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
