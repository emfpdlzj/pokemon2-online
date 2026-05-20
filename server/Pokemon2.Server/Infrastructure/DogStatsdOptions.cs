namespace Pokemon2.Server.Infrastructure;

public sealed class DogStatsdOptions
{
    public const string DefaultServiceName = "pokemon2-online-server";

    public bool Enabled { get; init; }
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 8125;
    public TimeSpan PublishInterval { get; init; } = TimeSpan.FromSeconds(10);
    public string Service { get; init; } = DefaultServiceName;
    public string? Environment { get; init; }
    public string? Version { get; init; }

    public static DogStatsdOptions FromEnvironment(IConfiguration configuration)
    {
        var host = FirstNonEmpty(
            configuration["POKEMON2_DOGSTATSD_HOST"],
            configuration["DD_AGENT_HOST"],
            "127.0.0.1");
        var enabledValue = FirstNonEmpty(
            configuration["POKEMON2_DOGSTATSD_ENABLED"],
            configuration["DD_DOGSTATSD_ENABLED"]);
        var enabled = bool.TryParse(enabledValue, out var parsedEnabled)
            ? parsedEnabled
            : !string.IsNullOrWhiteSpace(configuration["DD_AGENT_HOST"]);
        var port = GetInt(
            FirstNonEmpty(configuration["POKEMON2_DOGSTATSD_PORT"], configuration["DD_DOGSTATSD_PORT"]),
            8125);
        var intervalSeconds = GetInt(configuration["POKEMON2_METRICS_INTERVAL_SECONDS"], 10);

        return new DogStatsdOptions
        {
            Enabled = enabled,
            Host = host,
            Port = port,
            PublishInterval = TimeSpan.FromSeconds(Math.Max(1, intervalSeconds)),
            Service = FirstNonEmpty(configuration["DD_SERVICE"], DefaultServiceName),
            Environment = EmptyToNull(configuration["DD_ENV"]),
            Version = EmptyToNull(configuration["DD_VERSION"])
        };
    }

    private static int GetInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.First(value => !string.IsNullOrWhiteSpace(value))!;
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
