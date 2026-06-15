using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pokemon2.Server.Data;
using Pokemon2.Server.Game;
using Pokemon2.Server.Infrastructure;

namespace Pokemon2.Server.Tests.Game;

public sealed class RoomActorTests
{
    [Fact]
    public async Task Move_WhenSentTooFast_RejectsWithSpeedHackDetected()
    {
        var room = CreateRoom();
        var socket = new CapturingWebSocket();
        await JoinAsync(room, socket);

        await room.EnqueueAsync(new RoomCommand.Move("p1", Direction.Right, 1, CancellationToken.None));
        await socket.WaitForEnvelopeAsync("player_moved");

        await room.EnqueueAsync(new RoomCommand.Move("p1", Direction.Left, 2, CancellationToken.None));

        var rejected = await socket.WaitForEnvelopeAsync("move_rejected");
        Assert.Equal("speed_hack_detected", rejected["payload"]?["reason"]?.GetValue<string>());
    }

    [Fact]
    public async Task ToSummary_IncludesRoomMoveMetrics()
    {
        var room = CreateRoom();
        var p1 = new CapturingWebSocket();
        var p2 = new CapturingWebSocket();
        await JoinAsync(room, p1);
        await JoinAsync(room, p2, "p2");

        await room.EnqueueAsync(new RoomCommand.Move("p2", Direction.Right, 1, CancellationToken.None));
        await p2.WaitForEnvelopeAsync("move_rejected");

        await room.EnqueueAsync(new RoomCommand.Move("p1", Direction.Right, 1, CancellationToken.None));
        await p1.WaitForEnvelopeAsync("player_moved");

        await room.EnqueueAsync(new RoomCommand.Move("p1", Direction.Left, 1, CancellationToken.None));
        await p1.WaitForEnvelopeAsync("move_rejected");

        await room.EnqueueAsync(new RoomCommand.Move("p1", Direction.Left, 2, CancellationToken.None));
        await p1.WaitForEnvelopeAsync("move_rejected");

        var summary = room.ToSummary();
        Assert.Equal(2, summary.PlayerCount);
        Assert.Equal(1, summary.AcceptedMoves);
        Assert.Equal(3, summary.RejectedMoves);
        Assert.Equal(1, summary.RejectedMoveReasons.StaleSequence);
        Assert.Equal(1, summary.RejectedMoveReasons.SpeedHackDetected);
        Assert.Equal(1, summary.RejectedMoveReasons.TileOccupied);
        Assert.True(summary.AverageCommandLatencyMs >= 0);
        Assert.True(summary.MaxCommandLatencyMs >= 0);
    }

    [Fact]
    public async Task Attack_WhenTargetOutsideSkillRange_RejectsOutOfRange()
    {
        var room = CreateRoom();
        var socket = new CapturingWebSocket();
        await JoinAsync(room, socket);

        await room.EnqueueAsync(new RoomCommand.Attack("p1", "mon-1", "basic", 1, CancellationToken.None));

        var rejected = await socket.WaitForEnvelopeAsync("attack_rejected");
        Assert.Equal("out_of_range", rejected["payload"]?["reason"]?.GetValue<string>());
        Assert.Equal("mon-1", rejected["payload"]?["monsterId"]?.GetValue<string>());
    }

    [Fact]
    public async Task Attack_BeforeNextTurn_RejectsNotPlayerTurn()
    {
        var room = CreateRoom();
        var socket = new CapturingWebSocket();
        await JoinAsync(room, socket);

        await room.EnqueueAsync(new RoomCommand.Attack("p1", "mon-1", "ember", 1, CancellationToken.None));
        await socket.WaitForEnvelopeAsync("battle_result");

        await room.EnqueueAsync(new RoomCommand.Attack("p1", "mon-1", "ember", 2, CancellationToken.None));

        var rejected = await socket.WaitForEnvelopeAsync("attack_rejected");
        Assert.Equal("not_player_turn", rejected["payload"]?["reason"]?.GetValue<string>());
    }

    [Fact]
    public async Task Attack_WhenMonsterDefeated_SavesBattleResult()
    {
        var store = new CapturingBattleResultStore();
        var room = CreateRoom(store);
        var socket = new CapturingWebSocket();
        await JoinAsync(room, socket);

        await room.EnqueueAsync(new RoomCommand.Attack("p1", "mon-1", "ember", 1, CancellationToken.None));
        await socket.WaitForEnvelopeAsync("battle_result");

        for (var i = 0; i < 40; i++)
        {
            await room.EnqueueAsync(new RoomCommand.Tick());
        }

        await room.EnqueueAsync(new RoomCommand.Attack("p1", "mon-1", "ember", 2, CancellationToken.None));
        var ended = await socket.WaitForEnvelopeAsync("battle_ended");

        Assert.True(ended["payload"]?["won"]?.GetValue<bool>());
        var result = Assert.Single(store.Results);
        Assert.Equal("room-test", result.RoomId);
        Assert.Equal("p1", result.PlayerId);
        Assert.Equal("mon-1", result.MonsterId);
        Assert.True(result.Won);
    }

    [Fact]
    public async Task Rejoin_WithSamePlayerId_PreservesPositionAndIgnoresOldLeave()
    {
        var room = CreateRoom();
        var firstSocket = new CapturingWebSocket();
        await JoinAsync(room, firstSocket);

        await room.EnqueueAsync(new RoomCommand.Move("p1", Direction.Right, 1, CancellationToken.None));
        await firstSocket.WaitForEnvelopeAsync("player_moved");

        var reconnectSocket = new CapturingWebSocket();
        await room.EnqueueAsync(new RoomCommand.Join("p1", "session-2", "tester", reconnectSocket, CancellationToken.None));
        var joined = await reconnectSocket.WaitForEnvelopeAsync("joined");

        Assert.Equal(2, joined["payload"]?["position"]?["x"]?.GetValue<int>());
        Assert.Equal(1, joined["payload"]?["position"]?["y"]?.GetValue<int>());

        await room.EnqueueAsync(new RoomCommand.Leave("p1", "session-1"));
        Assert.Equal(1, room.ToSummary().PlayerCount);
    }

    private static RoomActor CreateRoom(IBattleResultStore? store = null)
    {
        var map = new GameMap("test-map", "Test Map", 8, 8, Array.Empty<Position>())
        {
            SpawnPoint = new Position(1, 1)
        };

        return new RoomActor("room-test", "Test Room", map, new ServerMetrics(), store ?? new NoopBattleResultStore());
    }

    private static async Task JoinAsync(RoomActor room, CapturingWebSocket socket, string playerId = "p1")
    {
        await room.EnqueueAsync(new RoomCommand.Join(playerId, "session-1", "tester", socket, CancellationToken.None));
        await socket.WaitForEnvelopeAsync("joined");
    }

    private sealed class CapturingBattleResultStore : IBattleResultStore
    {
        public List<BattleResult> Results { get; } = new();

        public Task SaveAsync(BattleResult result, CancellationToken cancellationToken)
        {
            Results.Add(result);
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingWebSocket : WebSocket
    {
        private readonly ConcurrentQueue<JsonNode> _messages = new();
        private readonly SemaphoreSlim _available = new(0);
        private WebSocketState _state = WebSocketState.Open;

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;

        public async Task<JsonNode> WaitForEnvelopeAsync(string type)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            while (!cts.IsCancellationRequested)
            {
                await _available.WaitAsync(cts.Token);
                while (_messages.TryDequeue(out var message))
                {
                    if (message["type"]?.GetValue<string>() == type)
                    {
                        return message;
                    }
                }
            }

            throw new TimeoutException($"Timed out waiting for {type}.");
        }

        public override void Abort()
        {
            _state = WebSocketState.Aborted;
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _state = WebSocketState.Closed;
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            var json = Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
            var node = JsonNode.Parse(json) ?? throw new JsonException("Empty JSON payload.");
            _messages.Enqueue(node);
            _available.Release();
            return Task.CompletedTask;
        }
    }
}
