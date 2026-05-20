namespace Pokemon2.Server.Game;

public readonly record struct Position(int X, int Y)
{
    public Position Move(Direction direction)
    {
        return direction switch
        {
            Direction.Up => this with { Y = Y - 1 },
            Direction.Down => this with { Y = Y + 1 },
            Direction.Left => this with { X = X - 1 },
            Direction.Right => this with { X = X + 1 },
            _ => this
        };
    }
}
