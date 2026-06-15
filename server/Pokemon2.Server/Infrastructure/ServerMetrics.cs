using System.Text.Json.Serialization;
using Pokemon2.Server.Llm;

namespace Pokemon2.Server.Infrastructure;

public sealed class ServerMetrics
{
    private long _roomsCreated;
    private long _joined;
    private long _left;
    private long _acceptedMoves;
    private long _rejectedMoves;
    private long _rejectedTileOccupied;
    private long _rejectedSpeedHackDetected;
    private long _rejectedStaleSequence;
    private long _ticks;
    private long _activeBattles;
    private long _completedBattles;
    private long _commandLatencyTotalTicks;
    private long _commandLatencySamples;
    private long _maxCommandLatencyTicks;
    private long _tickDelayTotalTicks;
    private long _tickDelaySamples;
    private long _maxTickDelayTicks;
    private long _llmReplyRequests;
    private long _llmReplySuccess;
    private long _llmReplyFallbacks;
    private long _llmChoicesRequests;
    private long _llmChoicesSuccess;
    private long _llmChoicesFallbacks;
    private long _llmRateLimited;
    private long _llmProviderErrors;
    private long _llmInvalidResponses;
    private long _llmNotConfigured;
    private long _llmPromptTokens;
    private long _llmCompletionTokens;
    private long _llmTotalTokens;
    private long _llmEstimatedCostMicros;

    public void IncrementRoomCreated() => Interlocked.Increment(ref _roomsCreated);
    public void IncrementJoined() => Interlocked.Increment(ref _joined);
    public void IncrementLeft() => Interlocked.Increment(ref _left);
    public void IncrementAcceptedMove() => Interlocked.Increment(ref _acceptedMoves);
    public void IncrementRejectedMove(string reason)
    {
        Interlocked.Increment(ref _rejectedMoves);
        IncrementRejectedReason(reason);
    }

    public void IncrementTick() => Interlocked.Increment(ref _ticks);
    public void IncrementActiveBattles() => Interlocked.Increment(ref _activeBattles);
    public void DecrementActiveBattles() => Interlocked.Decrement(ref _activeBattles);
    public void IncrementCompletedBattles() => Interlocked.Increment(ref _completedBattles);

    public void RecordCommandLatency(TimeSpan latency)
    {
        Interlocked.Add(ref _commandLatencyTotalTicks, latency.Ticks);
        Interlocked.Increment(ref _commandLatencySamples);
        SetMax(ref _maxCommandLatencyTicks, latency.Ticks);
    }

    public void RecordTickDelay(TimeSpan delay)
    {
        var ticks = Math.Max(0, delay.Ticks);
        Interlocked.Add(ref _tickDelayTotalTicks, ticks);
        Interlocked.Increment(ref _tickDelaySamples);
        SetMax(ref _maxTickDelayTicks, ticks);
    }

    public void RecordLlmResult(LlmOperation operation, bool usedFallback, string? failureReason, LlmCompletionUsage? usage)
    {
        if (operation == LlmOperation.Reply)
        {
            Interlocked.Increment(ref _llmReplyRequests);
            Interlocked.Increment(ref usedFallback ? ref _llmReplyFallbacks : ref _llmReplySuccess);
        }
        else
        {
            Interlocked.Increment(ref _llmChoicesRequests);
            Interlocked.Increment(ref usedFallback ? ref _llmChoicesFallbacks : ref _llmChoicesSuccess);
        }

        switch (failureReason)
        {
            case "rate_limited":
                Interlocked.Increment(ref _llmRateLimited);
                break;
            case "provider_error":
                Interlocked.Increment(ref _llmProviderErrors);
                break;
            case "invalid_response":
                Interlocked.Increment(ref _llmInvalidResponses);
                break;
            case "not_configured":
                Interlocked.Increment(ref _llmNotConfigured);
                break;
        }

        if (usage is null)
        {
            return;
        }

        Interlocked.Add(ref _llmPromptTokens, usage.PromptTokens);
        Interlocked.Add(ref _llmCompletionTokens, usage.CompletionTokens);
        Interlocked.Add(ref _llmTotalTokens, usage.TotalTokens);
        Interlocked.Add(ref _llmEstimatedCostMicros, (long)decimal.Round(usage.EstimatedCostUsd * 1_000_000m, 0, MidpointRounding.AwayFromZero));
    }

    public ServerMetricsSnapshot GetSnapshot()
    {
        var avgCommandLatencyMs = GetAverageMilliseconds(
            Interlocked.Read(ref _commandLatencyTotalTicks),
            Interlocked.Read(ref _commandLatencySamples));
        var avgTickDelayMs = GetAverageMilliseconds(
            Interlocked.Read(ref _tickDelayTotalTicks),
            Interlocked.Read(ref _tickDelaySamples));

        return new ServerMetricsSnapshot(
            Interlocked.Read(ref _roomsCreated),
            Interlocked.Read(ref _joined),
            Interlocked.Read(ref _left),
            Interlocked.Read(ref _acceptedMoves),
            Interlocked.Read(ref _rejectedMoves),
            new ServerRejectedMoveReasonCounts(
                Interlocked.Read(ref _rejectedTileOccupied),
                Interlocked.Read(ref _rejectedSpeedHackDetected),
                Interlocked.Read(ref _rejectedStaleSequence)),
            Interlocked.Read(ref _ticks),
            Interlocked.Read(ref _activeBattles),
            Interlocked.Read(ref _completedBattles),
            avgCommandLatencyMs,
            Milliseconds(Interlocked.Read(ref _maxCommandLatencyTicks)),
            avgTickDelayMs,
            Milliseconds(Interlocked.Read(ref _maxTickDelayTicks)),
            new LlmMetricsSnapshot(
                Interlocked.Read(ref _llmReplyRequests),
                Interlocked.Read(ref _llmReplySuccess),
                Interlocked.Read(ref _llmReplyFallbacks),
                Interlocked.Read(ref _llmChoicesRequests),
                Interlocked.Read(ref _llmChoicesSuccess),
                Interlocked.Read(ref _llmChoicesFallbacks),
                new LlmFailureReasonCounts(
                    Interlocked.Read(ref _llmRateLimited),
                    Interlocked.Read(ref _llmProviderErrors),
                    Interlocked.Read(ref _llmInvalidResponses),
                    Interlocked.Read(ref _llmNotConfigured)),
                Interlocked.Read(ref _llmPromptTokens),
                Interlocked.Read(ref _llmCompletionTokens),
                Interlocked.Read(ref _llmTotalTokens),
                Math.Round(Interlocked.Read(ref _llmEstimatedCostMicros) / 1_000_000d, 6)));
    }

    private void IncrementRejectedReason(string reason)
    {
        switch (reason)
        {
            case "tile_occupied":
                Interlocked.Increment(ref _rejectedTileOccupied);
                break;
            case "speed_hack_detected":
                Interlocked.Increment(ref _rejectedSpeedHackDetected);
                break;
            case "stale_sequence":
                Interlocked.Increment(ref _rejectedStaleSequence);
                break;
        }
    }

    private static double GetAverageMilliseconds(long totalTicksField, long samplesField)
    {
        return samplesField == 0 ? 0 : Milliseconds(totalTicksField / samplesField);
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
}

public sealed record ServerMetricsSnapshot(
    long RoomsCreated,
    long Joined,
    long Left,
    long AcceptedMoves,
    long RejectedMoves,
    ServerRejectedMoveReasonCounts RejectedMoveReasons,
    long Ticks,
    long ActiveBattles,
    long CompletedBattles,
    double AverageCommandLatencyMs,
    double MaxCommandLatencyMs,
    double AverageTickDelayMs,
    double MaxTickDelayMs,
    LlmMetricsSnapshot Llm);

public sealed record ServerRejectedMoveReasonCounts(
    [property: JsonPropertyName("tile_occupied")]
    long TileOccupied,
    [property: JsonPropertyName("speed_hack_detected")]
    long SpeedHackDetected,
    [property: JsonPropertyName("stale_sequence")]
    long StaleSequence);

public sealed record LlmMetricsSnapshot(
    long ReplyRequests,
    long ReplySuccess,
    long ReplyFallbacks,
    long ChoicesRequests,
    long ChoicesSuccess,
    long ChoicesFallbacks,
    LlmFailureReasonCounts FailureReasons,
    long PromptTokens,
    long CompletionTokens,
    long TotalTokens,
    double EstimatedCostUsd);

public sealed record LlmFailureReasonCounts(
    [property: JsonPropertyName("rate_limited")]
    long RateLimited,
    [property: JsonPropertyName("provider_error")]
    long ProviderError,
    [property: JsonPropertyName("invalid_response")]
    long InvalidResponse,
    [property: JsonPropertyName("not_configured")]
    long NotConfigured);
