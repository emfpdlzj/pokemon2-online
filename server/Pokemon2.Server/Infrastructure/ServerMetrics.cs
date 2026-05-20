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

    public object GetSnapshot()
    {
        var avgCommandLatencyMs = GetAverageMilliseconds(
            Interlocked.Read(ref _commandLatencyTotalTicks),
            Interlocked.Read(ref _commandLatencySamples));
        var avgTickDelayMs = GetAverageMilliseconds(
            Interlocked.Read(ref _tickDelayTotalTicks),
            Interlocked.Read(ref _tickDelaySamples));

        return new
        {
            roomsCreated = Interlocked.Read(ref _roomsCreated),
            joined = Interlocked.Read(ref _joined),
            left = Interlocked.Read(ref _left),
            acceptedMoves = Interlocked.Read(ref _acceptedMoves),
            rejectedMoves = Interlocked.Read(ref _rejectedMoves),
            rejectedMoveReasons = new
            {
                tile_occupied = Interlocked.Read(ref _rejectedTileOccupied),
                speed_hack_detected = Interlocked.Read(ref _rejectedSpeedHackDetected),
                stale_sequence = Interlocked.Read(ref _rejectedStaleSequence)
            },
            ticks = Interlocked.Read(ref _ticks),
            activeBattles = Interlocked.Read(ref _activeBattles),
            completedBattles = Interlocked.Read(ref _completedBattles),
            averageCommandLatencyMs = avgCommandLatencyMs,
            maxCommandLatencyMs = Milliseconds(Interlocked.Read(ref _maxCommandLatencyTicks)),
            averageTickDelayMs = avgTickDelayMs,
            maxTickDelayMs = Milliseconds(Interlocked.Read(ref _maxTickDelayTicks))
        };
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
