using System.Text.Json;
using System.Text.Json.Nodes;
using Pokemon2.Server.Infrastructure;

namespace Pokemon2.Server.Tests.Infrastructure;

public sealed class ServerMetricsTests
{
    [Fact]
    public void GetSnapshot_IncludesRejectedReasonsAndLatencyMaxes()
    {
        var metrics = new ServerMetrics();

        metrics.IncrementAcceptedMove();
        metrics.IncrementRejectedMove("tile_occupied");
        metrics.IncrementRejectedMove("speed_hack_detected");
        metrics.IncrementRejectedMove("stale_sequence");
        metrics.RecordCommandLatency(TimeSpan.FromMilliseconds(2));
        metrics.RecordCommandLatency(TimeSpan.FromMilliseconds(5));
        metrics.RecordTickDelay(TimeSpan.FromMilliseconds(1));
        metrics.RecordTickDelay(TimeSpan.FromMilliseconds(4));

        var snapshot = ToNode(metrics.GetSnapshot());

        Assert.Equal(1, snapshot["acceptedMoves"]?.GetValue<long>());
        Assert.Equal(3, snapshot["rejectedMoves"]?.GetValue<long>());
        Assert.Equal(1, snapshot["rejectedMoveReasons"]?["tile_occupied"]?.GetValue<long>());
        Assert.Equal(1, snapshot["rejectedMoveReasons"]?["speed_hack_detected"]?.GetValue<long>());
        Assert.Equal(1, snapshot["rejectedMoveReasons"]?["stale_sequence"]?.GetValue<long>());
        Assert.Equal(3.5, snapshot["averageCommandLatencyMs"]?.GetValue<double>());
        Assert.Equal(5, snapshot["maxCommandLatencyMs"]?.GetValue<double>());
        Assert.Equal(2.5, snapshot["averageTickDelayMs"]?.GetValue<double>());
        Assert.Equal(4, snapshot["maxTickDelayMs"]?.GetValue<double>());
    }

    private static JsonNode ToNode(object value)
    {
        return JsonSerializer.SerializeToNode(value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new JsonException("Snapshot did not serialize.");
    }
}
