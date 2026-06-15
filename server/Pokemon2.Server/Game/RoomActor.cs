using System.Net.WebSockets;
using System.Threading.Channels;
using Pokemon2.Server.Data;
using Pokemon2.Server.Infrastructure;
using Pokemon2.Server.Protocol;

namespace Pokemon2.Server.Game;

public sealed class RoomActor
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan MoveCooldown = TimeSpan.FromMilliseconds(100);
    private const string TileOccupiedReason = "tile_occupied";
    private const string SpeedHackDetectedReason = "speed_hack_detected";
    private const string StaleSequenceReason = "stale_sequence";
    private const int MonsterAiIntervalTicks = 20;

    private readonly Channel<RoomCommand> _commands = Channel.CreateUnbounded<RoomCommand>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly Dictionary<string, PlayerSession> _players = new();
    private readonly Dictionary<string, MonsterState> _monsters = new();
    private readonly Dictionary<string, ActiveBattle> _battles = new();
    private readonly ServerMetrics _metrics;
    private readonly IBattleResultStore _battleResults;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _commandLoop;
    private readonly Task _tickLoop;

    private long _tick;
    private long _acceptedMoves;
    private long _rejectedMoves;
    private long _rejectedTileOccupied;
    private long _rejectedSpeedHackDetected;
    private long _rejectedStaleSequence;
    private long _commandLatencyTotalTicks;
    private long _commandLatencySamples;
    private long _maxCommandLatencyTicks;
    private long _tickDelayTotalTicks;
    private long _tickDelaySamples;
    private long _maxTickDelayTicks;
    private bool _snapshotDirty;
    private DateTimeOffset _lastSnapshotAt = DateTimeOffset.MinValue;
    private DateTimeOffset? _lastTickAt;

    public RoomActor(string roomId, string roomName, GameMap map, ServerMetrics metrics, IBattleResultStore battleResults)
    {
        RoomId = roomId;
        RoomName = roomName;
        Map = map;
        _metrics = metrics;
        _battleResults = battleResults;
        CreatedAt = DateTimeOffset.UtcNow;
        SpawnMonsters();
        _commandLoop = Task.Run(RunCommandLoopAsync);
        _tickLoop = Task.Run(RunTickLoopAsync);
    }

    public string RoomId { get; }
    public string RoomName { get; }
    public GameMap Map { get; }
    public DateTimeOffset CreatedAt { get; }

    public RoomSummary ToSummary()
    {
        var commandLatencySamples = Interlocked.Read(ref _commandLatencySamples);
        var commandLatencyTotalTicks = Interlocked.Read(ref _commandLatencyTotalTicks);
        var tickDelaySamples = Interlocked.Read(ref _tickDelaySamples);
        var tickDelayTotalTicks = Interlocked.Read(ref _tickDelayTotalTicks);

        return new RoomSummary(
            RoomId,
            RoomName,
            Map.Id,
            Map.Name,
            _players.Count,
            4,
            _monsters.Values.Count(x => x.IsAlive),
            _battles.Count,
            Interlocked.Read(ref _tick),
            CreatedAt,
            Interlocked.Read(ref _acceptedMoves),
            Interlocked.Read(ref _rejectedMoves),
            new RejectedMoveReasonCounts(
                Interlocked.Read(ref _rejectedTileOccupied),
                Interlocked.Read(ref _rejectedSpeedHackDetected),
                Interlocked.Read(ref _rejectedStaleSequence)),
            AverageMilliseconds(commandLatencyTotalTicks, commandLatencySamples),
            Milliseconds(Interlocked.Read(ref _maxCommandLatencyTicks)),
            AverageMilliseconds(tickDelayTotalTicks, tickDelaySamples),
            Milliseconds(Interlocked.Read(ref _maxTickDelayTicks)));
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
                    case RoomCommand.Attack attack:
                        await HandleAttackAsync(attack);
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
                RecordCommandLatency(DateTimeOffset.UtcNow - startedAt);
            }
        }
    }

    private async Task HandleJoinAsync(RoomCommand.Join command)
    {
        var isReconnect = _players.TryGetValue(command.PlayerId, out var existingSession);
        if (!isReconnect && _players.Count >= 4)
        {
            await command.Socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Room is full", command.CancellationToken);
            return;
        }

        Position spawn;
        PlayerSession session;
        if (existingSession is not null)
        {
            try
            {
                if (existingSession.Socket.State == WebSocketState.Open)
                {
                    await existingSession.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnected from a newer session.", CancellationToken.None);
                }
            }
            catch
            {
                // Ignore old-socket close failures during reconnect replacement.
            }

            session = new PlayerSession(command.PlayerId, command.SessionId, command.PlayerName, command.Socket, existingSession.Position)
            {
                Facing = existingSession.Facing,
                LastSequence = existingSession.LastSequence,
                LastMoveAt = existingSession.LastMoveAt
            };
            spawn = session.Position;
        }
        else
        {
            var occupied = _players.Values.Select(player => player.Position).ToHashSet();
            spawn = FindSpawnPosition(occupied);
            session = new PlayerSession(command.PlayerId, command.SessionId, command.PlayerName, command.Socket, spawn);
        }

        _players[session.PlayerId] = session;
        _snapshotDirty = true;
        if (!isReconnect)
        {
            _metrics.IncrementJoined();
        }

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
        if (!_players.TryGetValue(command.PlayerId, out var session) || session.ConnectionId != command.SessionId)
        {
            return;
        }

        if (_players.Remove(command.PlayerId, out session))
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
            IncrementRejectedMove(StaleSequenceReason);
            await player.SendJsonAsync(MoveRejected(command, StaleSequenceReason, player.Position), command.CancellationToken);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - player.LastMoveAt < MoveCooldown)
        {
            IncrementRejectedMove(SpeedHackDetectedReason);
            await player.SendJsonAsync(MoveRejected(command, SpeedHackDetectedReason, player.Position), command.CancellationToken);
            return;
        }

        var next = player.Position.Move(command.Direction);
        var occupied = _players.Values
            .Where(other => other.PlayerId != player.PlayerId)
            .Select(other => other.Position)
            .ToHashSet();

        if (!Map.CanEnter(next))
        {
            IncrementRejectedMove("wall_collision");
            await player.SendJsonAsync(MoveRejected(command, "wall_collision", player.Position), command.CancellationToken);
            return;
        }

        if (occupied.Contains(next))
        {
            IncrementRejectedMove(TileOccupiedReason);
            await player.SendJsonAsync(MoveRejected(command, TileOccupiedReason, player.Position), command.CancellationToken);
            return;
        }

        player.Position = next;
        player.Facing = command.Direction;
        player.LastSequence = command.Sequence;
        player.LastMoveAt = now;
        _snapshotDirty = true;
        IncrementAcceptedMove();

        await BroadcastAsync(new ServerEnvelope("player_moved", new
        {
            playerId = player.PlayerId,
            sequence = command.Sequence,
            position = player.Position,
            facing = player.Facing,
            serverTick = _tick
        }));
    }

    private async Task HandleAttackAsync(RoomCommand.Attack command)
    {
        if (!_players.TryGetValue(command.PlayerId, out var player)) return;

        var battle = _battles.Values.FirstOrDefault(x => x.Player.PlayerId == command.PlayerId);
        var monster = battle?.Monster ?? FindTargetMonster(command.MonsterId, player.Position);
        if (monster is null)
        {
            await player.SendJsonAsync(AttackRejected(command, "target_not_found"), command.CancellationToken);
            return;
        }

        var skill = BattleRules.GetSkillOrDefault(command.SkillId);
        if (Distance(player.Position, monster.Position) > skill.Range)
        {
            await player.SendJsonAsync(AttackRejected(command, "out_of_range"), command.CancellationToken);
            return;
        }

        if (battle is null)
        {
            battle = new ActiveBattle($"battle-{Guid.NewGuid():N}"[..20], new BattleParticipant(player.PlayerId, player.Name), monster);
            _battles[battle.BattleId] = battle;
            _metrics.IncrementActiveBattles();
        }

        if (_tick < battle.NextPlayerTurnTick)
        {
            await player.SendJsonAsync(AttackRejected(command, "not_player_turn"), command.CancellationToken);
            return;
        }

        if (!BattleRules.TryResolvePlayerAttack(battle.Player, monster, command.SkillId, _tick, out var outcome, out var rejectReason))
        {
            await player.SendJsonAsync(AttackRejected(command, rejectReason), command.CancellationToken);
            return;
        }

        battle.NextPlayerTurnTick = _tick + MonsterAiIntervalTicks;
        _snapshotDirty = true;
        await BroadcastAsync(new ServerEnvelope("battle_result", new
        {
            battleId = battle.BattleId,
            playerId = player.PlayerId,
            monsterId = monster.MonsterId,
            skillId = outcome.SkillId,
            skillName = outcome.SkillName,
            damage = outcome.Damage,
            monsterHp = outcome.MonsterHp,
            playerHp = outcome.PlayerHp,
            playerMp = outcome.PlayerMp,
            won = outcome.MonsterDefeated,
            serverTick = _tick
        }));

        if (outcome.MonsterDefeated || battle.Player.Hp <= 0)
        {
            await CompleteBattleAsync(battle, outcome.MonsterDefeated, command.CancellationToken);
        }
    }

    private async Task HandleTickAsync()
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastTickAt is { } lastTickAt)
        {
            RecordTickDelay(now - lastTickAt - TickInterval);
        }

        _lastTickAt = now;
        Interlocked.Increment(ref _tick);
        _metrics.IncrementTick();
        UpdateMonsterAi();
        await SendSnapshotAsync(force: false);
    }

    private void IncrementAcceptedMove()
    {
        Interlocked.Increment(ref _acceptedMoves);
        _metrics.IncrementAcceptedMove();
    }

    private void IncrementRejectedMove(string reason)
    {
        Interlocked.Increment(ref _rejectedMoves);
        switch (reason)
        {
            case TileOccupiedReason:
                Interlocked.Increment(ref _rejectedTileOccupied);
                break;
            case SpeedHackDetectedReason:
                Interlocked.Increment(ref _rejectedSpeedHackDetected);
                break;
            case StaleSequenceReason:
                Interlocked.Increment(ref _rejectedStaleSequence);
                break;
        }

        _metrics.IncrementRejectedMove(reason);
    }

    private void RecordCommandLatency(TimeSpan latency)
    {
        Interlocked.Add(ref _commandLatencyTotalTicks, latency.Ticks);
        Interlocked.Increment(ref _commandLatencySamples);
        SetMax(ref _maxCommandLatencyTicks, latency.Ticks);
        _metrics.RecordCommandLatency(latency);
    }

    private void RecordTickDelay(TimeSpan delay)
    {
        var ticks = Math.Max(0, delay.Ticks);
        Interlocked.Add(ref _tickDelayTotalTicks, ticks);
        Interlocked.Increment(ref _tickDelaySamples);
        SetMax(ref _maxTickDelayTicks, ticks);
        _metrics.RecordTickDelay(TimeSpan.FromTicks(ticks));
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
            _players.Values.Select(player => player.ToSnapshot()).ToArray(),
            _monsters.Values.Select(monster => new MonsterSnapshot(
                monster.MonsterId,
                monster.Name,
                monster.Position,
                monster.Hp,
                monster.MaxHp,
                monster.IsAlive)).ToArray(),
            _battles.Values.Select(battle => new BattleSnapshot(
                battle.BattleId,
                battle.Player.PlayerId,
                battle.Monster.MonsterId,
                battle.Player.Hp,
                battle.Player.Mp,
                battle.Monster.Hp,
                battle.Monster.IsAlive && battle.Player.Hp > 0)).ToArray())));
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
            serverPosition = current
        });
    }

    private static ServerEnvelope AttackRejected(RoomCommand.Attack command, string reason)
    {
        return new ServerEnvelope("attack_rejected", new
        {
            sequence = command.Sequence,
            reason,
            monsterId = command.MonsterId
        });
    }

    private void SpawnMonsters()
    {
        var candidates = new[]
        {
            Map.SpawnPoint.Move(Direction.Right).Move(Direction.Right),
            Map.SpawnPoint.Move(Direction.Down).Move(Direction.Down),
            Map.SpawnPoint.Move(Direction.Left).Move(Direction.Left)
        };

        var index = 1;
        foreach (var position in candidates.Where(Map.CanEnter).Distinct().Take(3))
        {
            _monsters[$"mon-{index}"] = new MonsterState($"mon-{index}", index == 1 ? "풀벌레" : "꼬마새", position, 28 + index * 4, 3 + index);
            index++;
        }
    }

    private MonsterState? FindTargetMonster(string? monsterId, Position playerPosition)
    {
        if (!string.IsNullOrWhiteSpace(monsterId) &&
            _monsters.TryGetValue(monsterId, out var requested) &&
            requested.IsAlive)
        {
            return requested;
        }

        return _monsters.Values
            .Where(monster => monster.IsAlive)
            .OrderBy(monster => Distance(playerPosition, monster.Position))
            .FirstOrDefault();
    }

    private void UpdateMonsterAi()
    {
        if (_tick % MonsterAiIntervalTicks != 0 || _players.Count == 0) return;

        var occupiedByPlayers = _players.Values.Select(player => player.Position).ToHashSet();
        foreach (var monster in _monsters.Values.Where(monster => monster.IsAlive))
        {
            if (_battles.Values.Any(battle => battle.Monster.MonsterId == monster.MonsterId)) continue;

            var nearest = _players.Values
                .OrderBy(player => Distance(player.Position, monster.Position))
                .FirstOrDefault();
            if (nearest is null || Distance(nearest.Position, monster.Position) > 6) continue;

            var next = StepToward(monster.Position, nearest.Position);
            if (Map.CanEnter(next) && !occupiedByPlayers.Contains(next) && !_monsters.Values.Any(other => other.MonsterId != monster.MonsterId && other.Position == next))
            {
                monster.Position = next;
                _snapshotDirty = true;
            }
        }
    }

    private async Task CompleteBattleAsync(ActiveBattle battle, bool won, CancellationToken cancellationToken)
    {
        if (!_battles.Remove(battle.BattleId)) return;

        _metrics.DecrementActiveBattles();
        _metrics.IncrementCompletedBattles();
        await _battleResults.SaveAsync(new BattleResult
        {
            Id = Guid.NewGuid(),
            RoomId = RoomId,
            PlayerId = battle.Player.PlayerId,
            PlayerName = battle.Player.Name,
            MonsterId = battle.Monster.MonsterId,
            MonsterName = battle.Monster.Name,
            Won = won,
            ServerTick = checked((int)Math.Min(_tick, int.MaxValue)),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await BroadcastAsync(new ServerEnvelope("battle_ended", new
        {
            battleId = battle.BattleId,
            playerId = battle.Player.PlayerId,
            monsterId = battle.Monster.MonsterId,
            won,
            serverTick = _tick
        }));
    }

    private static int Distance(Position a, Position b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    private static double AverageMilliseconds(long totalTicks, long samples)
    {
        return samples == 0 ? 0 : Milliseconds(totalTicks / samples);
    }

    private static double Milliseconds(long ticks)
    {
        return Math.Round(TimeSpan.FromTicks(ticks).TotalMilliseconds, 3);
    }

    private static void SetMax(ref long target, long value)
    {
        var current = Interlocked.Read(ref target);
        while (value > current)
        {
            var original = Interlocked.CompareExchange(ref target, value, current);
            if (original == current) return;
            current = original;
        }
    }

    private static Position StepToward(Position from, Position to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy) && dx != 0)
        {
            return from.Move(dx > 0 ? Direction.Right : Direction.Left);
        }

        return dy == 0 ? from : from.Move(dy > 0 ? Direction.Down : Direction.Up);
    }

    private sealed class ActiveBattle
    {
        public ActiveBattle(string battleId, BattleParticipant player, MonsterState monster)
        {
            BattleId = battleId;
            Player = player;
            Monster = monster;
        }

        public string BattleId { get; }
        public BattleParticipant Player { get; }
        public MonsterState Monster { get; }
        public long NextPlayerTurnTick { get; set; }
    }
}
