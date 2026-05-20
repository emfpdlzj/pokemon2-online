using Pokemon2.Server.Infrastructure;

namespace Pokemon2.Server.Tests.Infrastructure;

public sealed class DogStatsdClientTests
{
    [Fact]
    public void FormatMetric_AddsGlobalAndMetricTags()
    {
        var metric = DogStatsdClient.FormatMetric(
            "pokemon2.moves.rejected.by_reason",
            3,
            "c",
            new[] { "service:pokemon2-online-server", "env:local" },
            new[] { "room_id:room-123", "reason:speed_hack_detected" });

        Assert.Equal(
            "pokemon2.moves.rejected.by_reason:3|c|#service:pokemon2-online-server,env:local,room_id:room-123,reason:speed_hack_detected",
            metric);
    }

    [Fact]
    public void FormatMetric_SanitizesUnsupportedCharacters()
    {
        var metric = DogStatsdClient.FormatMetric(
            "pokemon2 command latency",
            1.25,
            "g",
            Array.Empty<string>(),
            new[] { "room name:기본 방" });

        Assert.Equal("pokemon2_command_latency:1.25|g|#room_name:____", metric);
    }
}
