namespace Pokemon2.Server.Infrastructure;

public sealed class ServerMetrics
{
    private long _roomsCreated;
    private long _joined;
    private long _left;
    private long _acceptedMoves;
    private long _rejectedMoves;
    private long _ticks;
    private long _commandLatencyTotalTicks;
    private long _commandLatencySamples;

    public void IncrementRoomCreated() => Interlocked.Increment(ref _roomsCreated);
    public void IncrementJoined() => Interlocked.Increment(ref _joined);
    public void IncrementLeft() => Interlocked.Increment(ref _left);
    public void IncrementAcceptedMove() => Interlocked.Increment(ref _acceptedMoves);
    public void IncrementRejectedMove() => Interlocked.Increment(ref _rejectedMoves);
    public void IncrementTick() => Interlocked.Increment(ref _ticks);

    public void RecordCommandLatency(TimeSpan latency)
    {
        Interlocked.Add(ref _commandLatencyTotalTicks, latency.Ticks);
        Interlocked.Increment(ref _commandLatencySamples);
    }

    public object GetSnapshot()
    {
        var samples = Interlocked.Read(ref _commandLatencySamples);
        var totalTicks = Interlocked.Read(ref _commandLatencyTotalTicks);
        var avgMs = samples == 0 ? 0 : TimeSpan.FromTicks(totalTicks / samples).TotalMilliseconds;

        return new
        {
            roomsCreated = Interlocked.Read(ref _roomsCreated),
            joined = Interlocked.Read(ref _joined),
            left = Interlocked.Read(ref _left),
            acceptedMoves = Interlocked.Read(ref _acceptedMoves),
            rejectedMoves = Interlocked.Read(ref _rejectedMoves),
            ticks = Interlocked.Read(ref _ticks),
            averageCommandLatencyMs = Math.Round(avgMs, 3)
        };
    }
}
