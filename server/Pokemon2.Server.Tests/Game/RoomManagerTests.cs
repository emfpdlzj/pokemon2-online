using Pokemon2.Server.Game;
using Pokemon2.Server.Infrastructure;

namespace Pokemon2.Server.Tests.Game;

public sealed class RoomManagerTests
{
    [Fact]
    public void Constructor_CreatesDefaultRoom()
    {
        var manager = CreateManager();

        var rooms = manager.ListRooms();
        var room = Assert.Single(rooms);
        Assert.Equal("기본 방", room.RoomName);
        Assert.Equal("hometown", room.MapId);
        Assert.Equal("시작 마을", room.MapName);
        Assert.Equal(0, room.PlayerCount);
        Assert.Equal(4, room.MaxPlayers);
        Assert.True(room.MonsterCount > 0);
        Assert.Equal(0, room.ActiveBattleCount);
    }

    [Fact]
    public void CreateRoom_WithBlankNameAndUnknownMap_UsesDefaults()
    {
        var manager = CreateManager();

        var summary = manager.CreateRoom("   ", "unknown-map");

        Assert.Equal("방 2", summary.RoomName);
        Assert.Equal("hometown", summary.MapId);
        Assert.Equal("시작 마을", summary.MapName);
    }

    [Fact]
    public void GetOrCreate_WithExistingRoomId_ReturnsThatRoom()
    {
        var manager = CreateManager();
        var created = manager.CreateRoom("Route Room", "route1");

        var room = manager.GetOrCreate(created.RoomId);

        Assert.Equal(created.RoomId, room.RoomId);
        Assert.Equal("Route Room", room.RoomName);
        Assert.Equal("route1", room.Map.Id);
    }

    [Fact]
    public void GetOrCreate_WithMissingRoomId_ReturnsDefaultRoom()
    {
        var manager = CreateManager();

        var room = manager.GetOrCreate("missing-room");

        Assert.Equal("기본 방", room.RoomName);
        Assert.Equal("hometown", room.Map.Id);
    }

    private static RoomManager CreateManager()
    {
        return new RoomManager(new MapCatalog(), new ServerMetrics());
    }
}
