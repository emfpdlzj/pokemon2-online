namespace Pokemon2.Server.Game;

using System.Text.Json;

public sealed class MapCatalog
{
    private static readonly HashSet<int> BlockedTileTypes = [1, 4];

    private readonly Dictionary<string, GameMap> _maps;

    public MapCatalog()
    {
        _maps = LoadMaps(ResolveDefaultMapPath());
    }

    internal MapCatalog(string mapDataPath)
    {
        _maps = LoadMaps(mapDataPath);
    }

    public GameMap GetOrDefault(string? mapId)
    {
        if (!string.IsNullOrWhiteSpace(mapId) && _maps.TryGetValue(mapId, out var map))
        {
            return map;
        }

        return _maps["hometown"];
    }

    private static Dictionary<string, GameMap> LoadMaps(string mapDataPath)
    {
        using var stream = File.OpenRead(mapDataPath);
        var definitions = JsonSerializer.Deserialize<Dictionary<string, MapDefinition>>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Map data is empty: {mapDataPath}");

        return definitions.Select(entry =>
        {
            var id = entry.Key;
            var definition = entry.Value;
            var blockedTiles = GetBlockedTiles(definition);
            var map = new GameMap(id, definition.Name, definition.Width, definition.Height, blockedTiles)
            {
                SpawnPoint = new Position(definition.PlayerStart.Tx, definition.PlayerStart.Ty)
            };

            return map;
        }).ToDictionary(map => map.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<Position> GetBlockedTiles(MapDefinition definition)
    {
        for (var y = 0; y < definition.Tiles.Count; y++)
        {
            var row = definition.Tiles[y];
            for (var x = 0; x < row.Count; x++)
            {
                if (BlockedTileTypes.Contains(row[x]))
                {
                    yield return new Position(x, y);
                }
            }
        }

        foreach (var npc in definition.Npcs)
        {
            yield return new Position(npc.Tx, npc.Ty);
        }
    }

    private static string ResolveDefaultMapPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "data", "maps.json"),
            Path.Combine(AppContext.BaseDirectory, "client", "data", "maps.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "client", "data", "maps.json")
        };

        var path = candidates.FirstOrDefault(File.Exists);
        if (path is not null)
        {
            return path;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            path = Path.Combine(directory.FullName, "client", "data", "maps.json");
            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find map data file.", "client/data/maps.json");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record MapDefinition(
        string Name,
        int Width,
        int Height,
        List<List<int>> Tiles,
        List<NpcDefinition> Npcs,
        PointDefinition PlayerStart);

    private sealed record NpcDefinition(int Tx, int Ty);

    private sealed record PointDefinition(int Tx, int Ty);
}
