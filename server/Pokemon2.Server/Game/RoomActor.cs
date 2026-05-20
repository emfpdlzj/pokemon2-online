using System.Net.WebSockets;
using System.Threading.Channels;
using Pokemon2.Server.Infrastructure;
using Pokemon2.Server.Protocol;

namespace Pokemon2.Server.Game;

public sealed class RoomActor
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan MoveCooldown = TimeSpan.FromMilliseconds(100);

    private readonly Channel<RoomCommand> _commands = Channel.CreateUnbounded<RoomCommand>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly Dictionary<string, PlayerSession> _players = new();
    private readonly ServerMetrics _metrics;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _commandLoop;
    private readonly Task _tickLoop;

    private long _tick;
    private bool _snapshotDirty;
    private DateTimeOffset _lastSnapshotAt = DateTimeOffset.MinValue;

    public RoomActor(string roomId, string roomName, GameMap map, ServerMetrics metrics)
    {
        RoomId = roomId;
        RoomName = roomName;
        Map = map;
        _metrics = metrics;
        CreatedAt = DateTimeOffset.UtcNow;
        _commandLoop = Task.Run(RunCommandLoopAsync);
        _tickLoop = Task.Run(RunTickLoopAsync);
    }

    public string RoomId { get; }
    public string RoomName { get; }
    public GameMap Map { get; }
    public DateTimeOffset CreatedAt { get; }

    public RoomSummary ToSummary()
    {
        return new RoomSummary(RoomId, RoomName, Map.Id, Map.Name, _players.Count, 4, _tick, CreatedAt);
    }

    public ValueTask EnqueueAsync(RoomCommand command)
    {
        return _commands.Writer.WriteAsync(command, _shutdown.Token);
    }

    private async Task RunTickLoopAsync()
    {
        using var timer = new PeriodicTimer(TickInterval);
        while (!_shutdown.IsCancellationRequested && await timer.WaitForNextTickAsync(_shutdown.Token))
        {
            await EnqueueAsync(new RoomCommand.Tick());
        }
    }

    private async Task RunCommandLoopAsync()
    {
        await foreach (var command in _commands.Reader.ReadAllAsync(_shutdown.Token))
        {
            var startedAt = DateTimeOffset.UtcNow;
            try
            {
                switch (command)
                {
                    case RoomCommand.Join join:
                        await HandleJoinAsync(join);
                        break;
                    case RoomCommand.Leave leave:
                        await HandleLeaveAsync(leave);
                        break;
                    case RoomCommand.Move move:
                        await HandleMoveAsync(move);
                        break;
                    case RoomCommand.Chat chat:
                        await BroadcastAsync(new ServerEnvelope("chat", new
                        {
                            playerId = chat.PlayerId,
                            message = chat.Message
                        }));
                        break;
                    case RoomCommand.BattleEvent battle:
                        if (!_players.ContainsKey(battle.PlayerId)) break;
                        await BroadcastAsync(new ServerEnvelope("battle_event", new
                        {
                            playerId = battle.PlayerId,
                            payload = battle.Payload
                        }));
                        break;
                    case RoomCommand.Tick:
                        await HandleTickAsync();
                        break;
                }
            }
            finally
            {
                _metrics.RecordCommandLatency(DateTimeOffset.UtcNow - startedAt);
            }
        }
    }

    private async Task HandleJoinAsync(RoomCommand.Join command)
    {
        if (_players.Count >= 4)
        {
            await command.Socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Room is full", command.CancellationToken);
            return;
        }

        var occupied = _players.Values.Select(player => player.Position).ToHashSet();
        var spawn = FindSpawnPosition(occupied);
        var session = new PlayerSession(command.PlayerId, command.PlayerName, command.Socket, spawn);
        _players[session.PlayerId] = session;
        _snapshotDirty = true;
        _metrics.IncrementJoined();

        await session.SendJsonAsync(new ServerEnvelope("joined", new
        {
            roomId = RoomId,
            playerId = session.PlayerId,
            map = new { Map.Id, Map.Name, Map.Width, Map.Height },
            position = session.Position
        }), command.CancellationToken);

        await BroadcastAsync(new ServerEnvelope("player_joined", session.ToSnapshot()));
        await SendSnapshotAsync(force: true);
    }

    private async Task HandleLeaveAsync(RoomCommand.Leave command)
    {
        if (_players.Remove(command.PlayerId, out var session))
        {
            _snapshotDirty = true;
            _metrics.IncrementLeft();
            await BroadcastAsync(new ServerEnvelope("player_left", new
            {
                session.PlayerId,
                session.Name
            }));
        }
    }

    private async Task HandleMoveAsync(RoomCommand.Move command)
    {
        if (!_players.TryGetValue(command.PlayerId, out var player)) return;
        if (command.Sequence <= player.LastSequence)
        {
            _metrics.IncrementRejectedMove();
            await player.SendJsonAsync(MoveRejected(command, "stale_sequence", player.Position), command.CancellationToken);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - player.LastMoveAt < MoveCooldown)
        {
            _metrics.IncrementRejectedMove();
            await player.SendJsonAsync(MoveRejected(command, "rate_limited", player.Position), command.CancellationToken);
            return;
        }

        var next = player.Position.Move(command.Direction);
        var occupied = _players.Values
            .Where(other => other.PlayerId != player.PlayerId)
            .Select(other => other.Position)
            .ToHashSet();

        if (!Map.CanEnter(next) || occupied.Contains(next))
        {
            _metrics.IncrementRejectedMove();
            await player.SendJsonAsync(MoveRejected(command, "blocked", player.Position), command.CancellationToken);
            return;
        }

        player.Position = next;
        player.Facing = command.Direction;
        player.LastSequence = command.Sequence;
        player.LastMoveAt = now;
        _snapshotDirty = true;
        _metrics.IncrementAcceptedMove();

        await BroadcastAsync(new ServerEnvelope("player_moved", new
        {
            playerId = player.PlayerId,
            sequence = command.Sequence,
            position = player.Position,
            facing = player.Facing,
            serverTick = _tick
        }));
    }

    private async Task HandleTickAsync()
    {
        _tick++;
        _metrics.IncrementTick();
        await SendSnapshotAsync(force: false);
    }

    private async Task SendSnapshotAsync(bool force)
    {
        var now = DateTimeOffset.UtcNow;
        if (!force && !_snapshotDirty && now - _lastSnapshotAt < TimeSpan.FromMilliseconds(500))
        {
            return;
        }

        _snapshotDirty = false;
        _lastSnapshotAt = now;
        await BroadcastAsync(new ServerEnvelope("snapshot", new RoomSnapshot(
            RoomId,
            _tick,
            now.ToUnixTimeMilliseconds(),
            _players.Values.Select(player => player.ToSnapshot()).ToArray())));
    }

    private Position FindSpawnPosition(HashSet<Position> occupied)
    {
        var candidates = new[]
        {
            Map.SpawnPoint,
            Map.SpawnPoint.Move(Direction.Left),
            Map.SpawnPoint.Move(Direction.Right),
            Map.SpawnPoint.Move(Direction.Up),
            Map.SpawnPoint.Move(Direction.Down)
        };

        return candidates.First(position => Map.CanEnter(position) && !occupied.Contains(position));
    }

    private async Task BroadcastAsync(object payload)
    {
        foreach (var player in _players.Values.ToArray())
        {
            await player.SendJsonAsync(payload, _shutdown.Token);
        }
    }

    private static ServerEnvelope MoveRejected(RoomCommand.Move command, string reason, Position current)
    {
        return new ServerEnvelope("move_rejected", new
        {
            sequence = command.Sequence,
            reason,
            position = current
        });
    }
}
