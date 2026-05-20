namespace Pokemon2.Server.Game;

public sealed class MapCatalog
{
    private readonly Dictionary<string, GameMap> _maps;

    public MapCatalog()
    {
        var hometownBlocks = new List<Position>();
        for (var x = 0; x < 20; x++)
        {
            hometownBlocks.Add(new Position(x, 0));
            hometownBlocks.Add(new Position(x, 14));
        }

        for (var y = 0; y < 15; y++)
        {
            hometownBlocks.Add(new Position(0, y));
            hometownBlocks.Add(new Position(19, y));
        }

        hometownBlocks.AddRange(Rect(5, 5, 4, 4));
        hometownBlocks.AddRange(Rect(11, 10, 4, 3));

        var routeBlocks = new List<Position>();
        for (var x = 0; x < 20; x++)
        {
            routeBlocks.Add(new Position(x, 0));
            routeBlocks.Add(new Position(x, 39));
        }

        for (var y = 0; y < 40; y++)
        {
            routeBlocks.Add(new Position(0, y));
            routeBlocks.Add(new Position(19, y));
        }

        _maps = new[]
        {
            new GameMap("hometown", "시작 마을", 20, 15, hometownBlocks) { SpawnPoint = new Position(9, 9) },
            new GameMap("route1", "1번 도로", 20, 40, routeBlocks) { SpawnPoint = new Position(1, 7) }
        }.ToDictionary(map => map.Id, StringComparer.OrdinalIgnoreCase);
    }

    public GameMap GetOrDefault(string? mapId)
    {
        if (!string.IsNullOrWhiteSpace(mapId) && _maps.TryGetValue(mapId, out var map))
        {
            return map;
        }

        return _maps["hometown"];
    }

    private static IEnumerable<Position> Rect(int x, int y, int width, int height)
    {
        for (var ty = y; ty < y + height; ty++)
        {
            for (var tx = x; tx < x + width; tx++)
            {
                yield return new Position(tx, ty);
            }
        }
    }
}
