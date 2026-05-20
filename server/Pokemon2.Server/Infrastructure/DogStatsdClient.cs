using System.Globalization;
using System.Net.Sockets;
using System.Text;

namespace Pokemon2.Server.Infrastructure;

public sealed class DogStatsdClient : IDisposable
{
    private readonly UdpClient _udp;
    private readonly DogStatsdOptions _options;
    private readonly string[] _globalTags;

    public DogStatsdClient(DogStatsdOptions options)
    {
        _options = options;
        _udp = new UdpClient();
        _udp.Connect(options.Host, options.Port);
        _globalTags = BuildGlobalTags(options).ToArray();
    }

    public ValueTask GaugeAsync(string name, double value, IReadOnlyCollection<string>? tags = null, CancellationToken cancellationToken = default)
    {
        return SendAsync(name, value, "g", tags, cancellationToken);
    }

    public ValueTask CountAsync(string name, long value, IReadOnlyCollection<string>? tags = null, CancellationToken cancellationToken = default)
    {
        if (value <= 0) return ValueTask.CompletedTask;
        return SendAsync(name, value, "c", tags, cancellationToken);
    }

    public void Dispose()
    {
        _udp.Dispose();
    }

    public static string FormatMetric(string name, double value, string type, IReadOnlyCollection<string>? globalTags, IReadOnlyCollection<string>? tags)
    {
        var metric = $"{SanitizeMetricName(name)}:{value.ToString(CultureInfo.InvariantCulture)}|{type}";
        var allTags = (globalTags ?? Array.Empty<string>())
            .Concat(tags ?? Array.Empty<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(SanitizeTag)
            .ToArray();

        return allTags.Length == 0 ? metric : $"{metric}|#{string.Join(",", allTags)}";
    }

    private async ValueTask SendAsync(string name, double value, string type, IReadOnlyCollection<string>? tags, CancellationToken cancellationToken)
    {
        var payload = FormatMetric(name, value, type, _globalTags, tags);
        var bytes = Encoding.UTF8.GetBytes(payload);
        await _udp.SendAsync(bytes, cancellationToken);
    }

    private static IEnumerable<string> BuildGlobalTags(DogStatsdOptions options)
    {
        yield return $"service:{options.Service}";
        if (!string.IsNullOrWhiteSpace(options.Environment)) yield return $"env:{options.Environment}";
        if (!string.IsNullOrWhiteSpace(options.Version)) yield return $"version:{options.Version}";
    }

    private static string SanitizeMetricName(string value)
    {
        return string.Concat(value.Select(character =>
            IsAsciiLetterOrDigit(character) || character is '.' or '_' ? character : '_'));
    }

    private static string SanitizeTag(string value)
    {
        return string.Concat(value.Select(character =>
            IsAsciiLetterOrDigit(character) || character is ':' or '.' or '_' or '-' or '/' ? character : '_'));
    }

    private static bool IsAsciiLetterOrDigit(char value)
    {
        return value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
    }
}
