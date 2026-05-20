using Pokemon2.Server.Game;

namespace Pokemon2.Server.Tests.Game;

public sealed class MapCatalogTests
{
    [Fact]
    public void GetOrDefault_WithKnownMapId_ReturnsMatchingMapCaseInsensitively()
    {
        var catalog = new MapCatalog();

        var map = catalog.GetOrDefault("ROUTE1");

        Assert.Equal("route1", map.Id);
        Assert.Equal("1번 도로", map.Name);
        Assert.Equal(20, map.Width);
        Assert.Equal(40, map.Height);
        Assert.Equal(new Position(1, 7), map.SpawnPoint);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("missing")]
    public void GetOrDefault_WithUnknownMapId_ReturnsHometown(string? mapId)
    {
        var catalog = new MapCatalog();

        var map = catalog.GetOrDefault(mapId);

        Assert.Equal("hometown", map.Id);
        Assert.Equal("시작 마을", map.Name);
        Assert.Equal(new Position(9, 9), map.SpawnPoint);
    }

    [Fact]
    public void Hometown_CanEnter_UsesClientMapCollisionRules()
    {
        var catalog = new MapCatalog();
        var map = catalog.GetOrDefault("hometown");

        Assert.False(map.CanEnter(new Position(0, 1)));
        Assert.False(map.CanEnter(new Position(5, 5)));
        Assert.False(map.CanEnter(new Position(3, 9)));
        Assert.True(map.CanEnter(new Position(19, 13)));
        Assert.True(map.CanEnter(new Position(9, 9)));
    }
}
