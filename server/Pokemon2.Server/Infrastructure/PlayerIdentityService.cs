using System.Security.Cryptography;
using System.Text;

namespace Pokemon2.Server.Infrastructure;

public sealed class PlayerIdentityService
{
    public const string HeaderName = "X-Player-Identity";
    public const string QueryName = "playerToken";

    private const string DevelopmentFallbackSecret = "pokemon2-development-player-identity-secret";
    private readonly byte[] _secret;

    public PlayerIdentityService(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredSecret = configuration["PlayerIdentity:Secret"]
            ?? configuration["PlayerIdentity__Secret"]
            ?? Environment.GetEnvironmentVariable("POKEMON2_PLAYER_IDENTITY_SECRET");

        if (string.IsNullOrWhiteSpace(configuredSecret))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException("Player identity secret is not configured. Set PlayerIdentity__Secret or POKEMON2_PLAYER_IDENTITY_SECRET.");
            }

            configuredSecret = DevelopmentFallbackSecret;
        }

        _secret = Encoding.UTF8.GetBytes(configuredSecret);
    }

    public PlayerIdentity Issue()
    {
        var userId = $"user_{Guid.NewGuid():N}";
        return new PlayerIdentity(userId, BuildToken(userId), DateTimeOffset.UtcNow);
    }

    public bool TryResolve(HttpContext context, out PlayerIdentity identity)
    {
        var token = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            token = context.Request.Query[QueryName].ToString();
        }

        if (TryValidate(token, out var userId))
        {
            identity = new PlayerIdentity(userId, token, DateTimeOffset.UtcNow);
            return true;
        }

        identity = default;
        return false;
    }

    public bool TryValidate(string? token, out string userId)
    {
        userId = string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var separatorIndex = token.LastIndexOf('.');
        if (separatorIndex <= 0 || separatorIndex == token.Length - 1)
        {
            return false;
        }

        var candidateUserId = token[..separatorIndex];
        var signature = token[(separatorIndex + 1)..];
        if (!IsSafeUserId(candidateUserId))
        {
            return false;
        }

        var expectedSignature = ComputeSignature(candidateUserId);
        var providedBytes = Encoding.UTF8.GetBytes(signature);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        if (!CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
        {
            return false;
        }

        userId = candidateUserId;
        return true;
    }

    public string BuildToken(string userId)
    {
        if (!IsSafeUserId(userId))
        {
            throw new ArgumentException("User id is invalid.", nameof(userId));
        }

        return $"{userId}.{ComputeSignature(userId)}";
    }

    private string ComputeSignature(string userId)
    {
        using var hmac = new HMACSHA256(_secret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(userId));
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool IsSafeUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || userId.Length > 80)
        {
            return false;
        }

        foreach (var ch in userId)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
            {
                continue;
            }

            return false;
        }

        return true;
    }
}

public readonly record struct PlayerIdentity(
    string UserId,
    string Token,
    DateTimeOffset IssuedAt);
