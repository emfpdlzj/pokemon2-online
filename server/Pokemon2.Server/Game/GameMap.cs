namespace Pokemon2.Server.Game;

public sealed class GameMap
{
    private readonly HashSet<Position> _blockedTiles;

    public GameMap(string id, string name, int width, int height, IEnumerable<Position> blockedTiles)
    {
        Id = id;
        Name = name;
        Width = width;
        Height = height;
        _blockedTiles = blockedTiles.ToHashSet();
    }

    public string Id { get; }
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }

    public Position SpawnPoint { get; init; } = new(9, 9);

    public bool CanEnter(Position position)
    {
        if (position.X < 0 || position.Y < 0) return false;
        if (position.X >= Width || position.Y >= Height) return false;
        return !_blockedTiles.Contains(position);
    }
}
