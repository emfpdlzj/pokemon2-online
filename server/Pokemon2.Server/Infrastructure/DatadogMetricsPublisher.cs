using Pokemon2.Server.Game;

namespace Pokemon2.Server.Infrastructure;

public sealed class DatadogMetricsPublisher : BackgroundService
{
    private readonly RoomManager _rooms;
    private readonly ServerMetrics _metrics;
    private readonly DogStatsdOptions _options;
    private readonly ILogger<DatadogMetricsPublisher> _logger;
    private ServerMetricsSnapshot? _previousTotals;
    private readonly Dictionary<string, RoomSummary> _previousRooms = new(StringComparer.OrdinalIgnoreCase);

    public DatadogMetricsPublisher(
        RoomManager rooms,
        ServerMetrics metrics,
        DogStatsdOptions options,
        ILogger<DatadogMetricsPublisher> logger)
    {
        _rooms = rooms;
        _metrics = metrics;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("DogStatsD metrics publisher is disabled.");
            return;
        }

        _logger.LogInformation(
            "DogStatsD metrics publisher is enabled. Target={Host}:{Port}, Interval={IntervalSeconds}s",
            _options.Host,
            _options.Port,
            _options.PublishInterval.TotalSeconds);

        using var client = new DogStatsdClient(_options);
        using var timer = new PeriodicTimer(_options.PublishInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await PublishOnceAsync(client, stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task PublishOnceAsync(DogStatsdClient client, CancellationToken cancellationToken)
    {
        try
        {
            var totals = _metrics.GetSnapshot();
            var rooms = _rooms.ListRooms();

            await PublishTotalsAsync(client, totals, rooms, cancellationToken);
            foreach (var room in rooms)
            {
                await PublishRoomAsync(client, room, cancellationToken);
            }

            _previousTotals = totals;
            _previousRooms.Clear();
            foreach (var room in rooms)
            {
                _previousRooms[room.RoomId] = room;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish DogStatsD metrics.");
        }
    }

    private async Task PublishTotalsAsync(DogStatsdClient client, ServerMetricsSnapshot totals, IReadOnlyCollection<RoomSummary> rooms, CancellationToken cancellationToken)
    {
        await client.GaugeAsync("pokemon2.room.count", rooms.Count, cancellationToken: cancellationToken);
        await client.GaugeAsync("pokemon2.player.count", rooms.Sum(room => room.PlayerCount), cancellationToken: cancellationToken);
        await client.GaugeAsync("pokemon2.battle.active", totals.ActiveBattles, cancellationToken: cancellationToken);
        await client.GaugeAsync("pokemon2.command.latency.avg_ms", totals.AverageCommandLatencyMs, cancellationToken: cancellationToken);
        await client.GaugeAsync("pokemon2.command.latency.max_ms", totals.MaxCommandLatencyMs, cancellationToken: cancellationToken);
        await client.GaugeAsync("pokemon2.tick.delay.avg_ms", totals.AverageTickDelayMs, cancellationToken: cancellationToken);
        await client.GaugeAsync("pokemon2.tick.delay.max_ms", totals.MaxTickDelayMs, cancellationToken: cancellationToken);
        await client.GaugeAsync("pokemon2.llm.tokens.prompt_total", totals.Llm.PromptTokens, cancellationToken: cancellationToken);
        await client.GaugeAsync("pokemon2.llm.tokens.completion_total", totals.Llm.CompletionTokens, cancellationToken: cancellationToken);
        await client.GaugeAsync("pokemon2.llm.tokens.total", totals.Llm.TotalTokens, cancellationToken: cancellationToken);
        await client.GaugeAsync("pokemon2.llm.cost.estimated_usd_total", totals.Llm.EstimatedCostUsd, cancellationToken: cancellationToken);

        var previous = _previousTotals ?? EmptyTotals;

        await client.CountAsync("pokemon2.moves.accepted", totals.AcceptedMoves - previous.AcceptedMoves, cancellationToken: cancellationToken);
        await client.CountAsync("pokemon2.moves.rejected", totals.RejectedMoves - previous.RejectedMoves, cancellationToken: cancellationToken);
        await CountRejectedReasonAsync(client, "tile_occupied", totals.RejectedMoveReasons.TileOccupied - previous.RejectedMoveReasons.TileOccupied, null, cancellationToken);
        await CountRejectedReasonAsync(client, "speed_hack_detected", totals.RejectedMoveReasons.SpeedHackDetected - previous.RejectedMoveReasons.SpeedHackDetected, null, cancellationToken);
        await CountRejectedReasonAsync(client, "stale_sequence", totals.RejectedMoveReasons.StaleSequence - previous.RejectedMoveReasons.StaleSequence, null, cancellationToken);
        await client.CountAsync("pokemon2.llm.reply.requests", totals.Llm.ReplyRequests - previous.Llm.ReplyRequests, cancellationToken: cancellationToken);
        await client.CountAsync("pokemon2.llm.reply.success", totals.Llm.ReplySuccess - previous.Llm.ReplySuccess, cancellationToken: cancellationToken);
        await client.CountAsync("pokemon2.llm.reply.fallback", totals.Llm.ReplyFallbacks - previous.Llm.ReplyFallbacks, cancellationToken: cancellationToken);
        await client.CountAsync("pokemon2.llm.choices.requests", totals.Llm.ChoicesRequests - previous.Llm.ChoicesRequests, cancellationToken: cancellationToken);
        await client.CountAsync("pokemon2.llm.choices.success", totals.Llm.ChoicesSuccess - previous.Llm.ChoicesSuccess, cancellationToken: cancellationToken);
        await client.CountAsync("pokemon2.llm.choices.fallback", totals.Llm.ChoicesFallbacks - previous.Llm.ChoicesFallbacks, cancellationToken: cancellationToken);
        await CountLlmFailureAsync(client, "rate_limited", totals.Llm.FailureReasons.RateLimited - previous.Llm.FailureReasons.RateLimited, cancellationToken);
        await CountLlmFailureAsync(client, "provider_error", totals.Llm.FailureReasons.ProviderError - previous.Llm.FailureReasons.ProviderError, cancellationToken);
        await CountLlmFailureAsync(client, "invalid_response", totals.Llm.FailureReasons.InvalidResponse - previous.Llm.FailureReasons.InvalidResponse, cancellationToken);
        await CountLlmFailureAsync(client, "not_configured", totals.Llm.FailureReasons.NotConfigured - previous.Llm.FailureReasons.NotConfigured, cancellationToken);
    }

    private async Task PublishRoomAsync(DogStatsdClient client, RoomSummary room, CancellationToken cancellationToken)
    {
        var tags = RoomTags(room);
        await client.GaugeAsync("pokemon2.room.player_count", room.PlayerCount, tags, cancellationToken);
        await client.GaugeAsync("pokemon2.room.monster_count", room.MonsterCount, tags, cancellationToken);
        await client.GaugeAsync("pokemon2.room.active_battle_count", room.ActiveBattleCount, tags, cancellationToken);
        await client.GaugeAsync("pokemon2.room.server_tick", room.ServerTick, tags, cancellationToken);
        await client.GaugeAsync("pokemon2.room.command.latency.avg_ms", room.AverageCommandLatencyMs, tags, cancellationToken);
        await client.GaugeAsync("pokemon2.room.command.latency.max_ms", room.MaxCommandLatencyMs, tags, cancellationToken);
        await client.GaugeAsync("pokemon2.room.tick.delay.avg_ms", room.AverageTickDelayMs, tags, cancellationToken);
        await client.GaugeAsync("pokemon2.room.tick.delay.max_ms", room.MaxTickDelayMs, tags, cancellationToken);

        _previousRooms.TryGetValue(room.RoomId, out var previous);

        await client.CountAsync("pokemon2.room.moves.accepted", room.AcceptedMoves - (previous?.AcceptedMoves ?? 0), tags, cancellationToken);
        await client.CountAsync("pokemon2.room.moves.rejected", room.RejectedMoves - (previous?.RejectedMoves ?? 0), tags, cancellationToken);
        await CountRejectedReasonAsync(client, "tile_occupied", room.RejectedMoveReasons.TileOccupied - (previous?.RejectedMoveReasons.TileOccupied ?? 0), tags, cancellationToken);
        await CountRejectedReasonAsync(client, "speed_hack_detected", room.RejectedMoveReasons.SpeedHackDetected - (previous?.RejectedMoveReasons.SpeedHackDetected ?? 0), tags, cancellationToken);
        await CountRejectedReasonAsync(client, "stale_sequence", room.RejectedMoveReasons.StaleSequence - (previous?.RejectedMoveReasons.StaleSequence ?? 0), tags, cancellationToken);
    }

    private static ValueTask CountRejectedReasonAsync(
        DogStatsdClient client,
        string reason,
        long count,
        IReadOnlyCollection<string>? tags,
        CancellationToken cancellationToken)
    {
        var reasonTags = (tags ?? Array.Empty<string>())
            .Concat(new[] { $"reason:{reason}" })
            .ToArray();
        return client.CountAsync("pokemon2.moves.rejected.by_reason", count, reasonTags, cancellationToken);
    }

    private static string[] RoomTags(RoomSummary room)
    {
        return new[]
        {
            $"room_id:{room.RoomId}",
            $"map_id:{room.MapId}"
        };
    }

    private static readonly ServerMetricsSnapshot EmptyTotals = new(
        0,
        0,
        0,
        0,
        0,
        new ServerRejectedMoveReasonCounts(0, 0, 0),
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        new LlmMetricsSnapshot(
            0,
            0,
            0,
            0,
            0,
            0,
            new LlmFailureReasonCounts(0, 0, 0, 0),
            0,
            0,
            0,
            0));

    private static ValueTask CountLlmFailureAsync(
        DogStatsdClient client,
        string reason,
        long count,
        CancellationToken cancellationToken)
    {
        return client.CountAsync("pokemon2.llm.failures", count, new[] { $"reason:{reason}" }, cancellationToken);
    }
}
