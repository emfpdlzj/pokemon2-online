using System.Text.Json;
using Pokemon2.Server.Data;

namespace Pokemon2.Server.Tests.Data;

public sealed class SaveMapperTests
{
    [Fact]
    public void Apply_WithOutOfRangeAndBlankValues_NormalizesSave()
    {
        var save = new PlayerSave
        {
            CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
        };
        var now = DateTimeOffset.Parse("2026-05-20T12:34:56Z");
        using var events = JsonDocument.Parse("""{"rivalDefeated":true}""");
        using var gameState = JsonDocument.Parse("""{"badges":2}""");
        var request = new UpsertSaveRequest(
            SlotNumber: 99,
            Mode: "COOP",
            PlayerName: "   ",
            CurrentMap: null,
            PositionX: 7,
            PositionY: 11,
            Starter: new SaveStarterDto("  pikachu  ", "  Pikachu  ", 5, 20),
            Events: events.RootElement.Clone(),
            GameState: gameState.RootElement.Clone(),
            PlayTimeSeconds: -10);

        SaveMapper.Apply(save, request, now);

        Assert.Equal(3, save.SlotNumber);
        Assert.Equal("single", save.Mode);
        Assert.Equal("주인공", save.PlayerName);
        Assert.Equal("hometown", save.CurrentMap);
        Assert.Equal(7, save.PositionX);
        Assert.Equal(11, save.PositionY);
        Assert.Equal("pikachu", save.StarterId);
        Assert.Equal("Pikachu", save.StarterName);
        Assert.Equal(5, save.StarterLevel);
        Assert.Equal(20, save.StarterCurrentHp);
        Assert.Equal("""{"rivalDefeated":true}""", save.EventsJson);
        Assert.Equal("""{"badges":2}""", save.GameStateJson);
        Assert.Equal(0, save.PlayTimeSeconds);
        Assert.Equal(now, save.UpdatedAt);
    }

    [Fact]
    public void ToDetail_WithSavedJson_ReturnsParsedPayloadAndStarter()
    {
        var save = new PlayerSave
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            SlotNumber = 2,
            Mode = "multi",
            PlayerName = "Leaf",
            CurrentMap = "route1",
            PositionX = 3,
            PositionY = 4,
            StarterId = "squirtle",
            StarterName = "Squirtle",
            StarterLevel = 8,
            StarterCurrentHp = 25,
            EventsJson = """{"metProfessor":true}""",
            GameStateJson = """{"inventory":["potion"]}""",
            PlayTimeSeconds = 123,
            CreatedAt = DateTimeOffset.Parse("2026-05-19T00:00:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-05-20T00:00:00Z")
        };

        var detail = SaveMapper.ToDetail(save);

        Assert.Equal(save.Id, detail.Id);
        Assert.Equal(2, detail.SlotNumber);
        Assert.Equal("multi", detail.Mode);
        Assert.Equal("Leaf", detail.PlayerName);
        Assert.Equal("route1", detail.CurrentMap);
        Assert.Equal(3, detail.PositionX);
        Assert.Equal(4, detail.PositionY);
        Assert.NotNull(detail.Starter);
        Assert.Equal("squirtle", detail.Starter.Id);
        Assert.True(detail.Events.GetProperty("metProfessor").GetBoolean());
        Assert.Equal("potion", detail.GameState.GetProperty("inventory")[0].GetString());
        Assert.Equal(123, detail.PlayTimeSeconds);
        Assert.Equal(save.CreatedAt, detail.CreatedAt);
        Assert.Equal(save.UpdatedAt, detail.UpdatedAt);
    }

    [Fact]
    public void ToSummary_WithoutStarterValues_ReturnsNullStarter()
    {
        var save = new PlayerSave
        {
            Id = Guid.Parse("20000000-0000-0000-0000-000000000002"),
            SlotNumber = 1,
            Mode = "single",
            PlayerName = "Red",
            CurrentMap = "hometown",
            UpdatedAt = DateTimeOffset.Parse("2026-05-20T00:00:00Z")
        };

        var summary = SaveMapper.ToSummary(save);

        Assert.Equal(save.Id, summary.Id);
        Assert.Null(summary.Starter);
    }
}
