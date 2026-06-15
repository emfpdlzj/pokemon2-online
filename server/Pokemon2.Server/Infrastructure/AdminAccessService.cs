using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Pokemon2.Server.Infrastructure;

public sealed class AdminAccessService
{
    public const string HeaderName = "X-Admin-Token";

    private readonly string? _token;
    private readonly HashSet<string> _allowlist;
    private readonly bool _allowLoopbackInDevelopment;
    private readonly IHostEnvironment _environment;

    public AdminAccessService(IConfiguration configuration, IHostEnvironment environment)
    {
        _environment = environment;
        _token = FirstNonEmpty(configuration["AdminApi:Token"], configuration["POKEMON2_ADMIN_TOKEN"]);
        _allowlist = (FirstNonEmpty(configuration["AdminApi:Allowlist"], configuration["POKEMON2_ADMIN_ALLOWLIST"]) ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _allowLoopbackInDevelopment = bool.TryParse(configuration["AdminApi:AllowLoopbackInDevelopment"], out var parsed)
            ? parsed
            : environment.IsDevelopment();
    }

    public bool IsAuthorized(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is not null)
        {
            if (_environment.IsDevelopment() && _allowLoopbackInDevelopment && IPAddress.IsLoopback(remoteIp))
            {
                return true;
            }

            if (_allowlist.Contains(remoteIp.ToString()))
            {
                return true;
            }
        }

        if (string.IsNullOrWhiteSpace(_token))
        {
            return false;
        }

        var presentedToken = context.Request.Headers[HeaderName].ToString().Trim();
        return FixedTimeEquals(presentedToken, _token);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
