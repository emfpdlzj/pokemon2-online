using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

const string PlayerIdentityHeaderName = "X-Player-Identity";
const string AdminTokenHeaderName = "X-Admin-Token";

var config = LoadTestConfig.Parse(args);
using var http = new HttpClient { BaseAddress = config.HttpBaseUri };

Console.WriteLine($"target={config.HttpBaseUri} scenario={config.Scenario} rooms={config.Rooms} clientsPerRoom={config.ClientsPerRoom} moves={config.MovesPerClient} delayMs={config.ArtificialDelayMs}");

var roomIds = new List<string>();
for (var i = 0; i < config.Rooms; i++)
{
    var response = await http.PostAsJsonAsync("/api/rooms", new { roomName = $"load-{config.Scenario}-{i + 1}", mapId = "hometown" });
    response.EnsureSuccessStatusCode();
    var room = await response.Content.ReadFromJsonAsync<RoomSummary>();
    roomIds.Add(room?.RoomId ?? throw new InvalidOperationException("Room creation response missing roomId."));
}

var stopwatch = Stopwatch.StartNew();
var tasks = roomIds.SelectMany((roomId, roomIndex) =>
    Enumerable.Range(0, config.ClientsPerRoom)
        .Select(clientIndex => RunClientAsync(config, http, roomId, roomIndex, clientIndex))).ToArray();

var results = await Task.WhenAll(tasks);
stopwatch.Stop();

var accepted = results.Sum(result => result.AcceptedMoves);
var rejected = results.Sum(result => result.RejectedMoves);
var snapshots = results.Sum(result => result.Snapshots);
var avgRtt = results.SelectMany(result => result.RoundTripTimes).DefaultIfEmpty(TimeSpan.Zero).Average(span => span.TotalMilliseconds);

Console.WriteLine("---- result ----");
Console.WriteLine($"elapsedMs={stopwatch.ElapsedMilliseconds}");
Console.WriteLine($"sentMoves={results.Sum(result => result.SentMoves)}");
Console.WriteLine($"acceptedMoves={accepted}");
Console.WriteLine($"rejectedMoves={rejected}");
Console.WriteLine($"snapshots={snapshots}");
Console.WriteLine($"avgObservedRttMs={Math.Round(avgRtt, 2)}");

Console.WriteLine("---- rejection reasons ----");
foreach (var item in results.SelectMany(result => result.RejectionReasons).GroupBy(item => item.Key).OrderBy(group => group.Key))
{
    Console.WriteLine($"{item.Key}={item.Sum(reason => reason.Value)}");
}

Console.WriteLine("---- client breakdown ----");
foreach (var group in results.GroupBy(result => result.Profile).OrderBy(group => group.Key))
{
    var groupRtt = group.SelectMany(result => result.RoundTripTimes).DefaultIfEmpty(TimeSpan.Zero).Average(span => span.TotalMilliseconds);
    var reasons = string.Join(", ", group
        .SelectMany(result => result.RejectionReasons)
        .GroupBy(item => item.Key)
        .OrderBy(item => item.Key)
        .Select(item => $"{item.Key}:{item.Sum(reason => reason.Value)}"));
    Console.WriteLine($"{group.Key}: clients={group.Count()} sent={group.Sum(x => x.SentMoves)} accepted={group.Sum(x => x.AcceptedMoves)} rejected={group.Sum(x => x.RejectedMoves)} avgRttMs={Math.Round(groupRtt, 2)} reasons=[{reasons}]");
}

var metricsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/metrics");
if (!string.IsNullOrWhiteSpace(config.AdminToken))
{
    metricsRequest.Headers.Add(AdminTokenHeaderName, config.AdminToken);
}

var metricsResponse = await http.SendAsync(metricsRequest);
Console.WriteLine("---- server metrics ----");
if (metricsResponse.IsSuccessStatusCode)
{
    var metrics = await metricsResponse.Content.ReadFromJsonAsync<JsonElement>();
    Console.WriteLine(JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
}
else
{
    Console.WriteLine($"metricsUnavailable={metricsResponse.StatusCode}");
}

static async Task<ClientResult> RunClientAsync(LoadTestConfig config, HttpClient http, string roomId, int roomIndex, int clientIndex)
{
    var playerName = $"bot-{roomIndex}-{clientIndex}-{GetProfile(config.Scenario, clientIndex)}";
    var identity = await http.GetFromJsonAsync<PlayerIdentityResponse>("/api/player/identity")
        ?? throw new InvalidOperationException("Player identity response was empty.");
    using var joinRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/rooms/{roomId}/join")
    {
        Content = JsonContent.Create(new { playerName })
    };
    joinRequest.Headers.Add(PlayerIdentityHeaderName, identity.Token);
    using var joinResponse = await http.SendAsync(joinRequest);
    joinResponse.EnsureSuccessStatusCode();
    var joined = await joinResponse.Content.ReadFromJsonAsync<JoinRoomResponse>()
        ?? throw new InvalidOperationException("Join room response was empty.");

    using var socket = new ClientWebSocket();
    var profile = GetProfile(config.Scenario, clientIndex);
    await socket.ConnectAsync(new Uri(joined.WsUrl), CancellationToken.None);

    var result = new ClientResult(profile);
    var receiveTask = Task.Run(() => ReceiveAsync(socket, result));

    await Task.Delay(300);

    switch (config.Scenario)
    {
        case LoadScenario.Collision:
            await RunCollisionScenarioAsync(socket, result, clientIndex, config);
            break;
        case LoadScenario.SyncPace:
            await RunSyncPaceScenarioAsync(socket, result, clientIndex, config);
            break;
        default:
            await RunDefaultScenarioAsync(socket, result, clientIndex, config);
            break;
    }

    await Task.Delay(1000);
    if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
    {
        try
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            // The server may already have completed the close path after the load scenario ends.
        }
    }

    await receiveTask;
    return result;
}

static async Task RunDefaultScenarioAsync(ClientWebSocket socket, ClientResult result, int clientIndex, LoadTestConfig config)
{
    var directions = new[] { "Left", "Right", "Up", "Down" };
    for (var sequence = 1; sequence <= config.MovesPerClient; sequence++)
    {
        if (config.ArtificialDelayMs > 0)
        {
            await Task.Delay(config.ArtificialDelayMs);
        }

        await SendMoveAsync(socket, result, directions[(sequence + clientIndex) % directions.Length], sequence);
        await Task.Delay(config.MoveIntervalMs);
    }
}

static async Task RunCollisionScenarioAsync(ClientWebSocket socket, ClientResult result, int clientIndex, LoadTestConfig config)
{
    await WaitForJoinAsync(result);

    var anchor = new TestPosition(9, 9);
    if (result.Position == anchor)
    {
        await Task.Delay(config.MoveIntervalMs * 2);
        return;
    }

    var direction = DirectionToward(result.Position, anchor);
    if (direction is null)
    {
        await Task.Delay(config.MoveIntervalMs * 2);
        return;
    }

    await Task.Delay(config.ArtificialDelayMs);
    await SendMoveAsync(socket, result, direction, 1);
}

static async Task RunSyncPaceScenarioAsync(ClientWebSocket socket, ClientResult result, int clientIndex, LoadTestConfig config)
{
    var paceMs = clientIndex switch
    {
        0 => config.FastMoveIntervalMs,
        2 => config.SlowMoveIntervalMs,
        _ => config.MoveIntervalMs
    };

    await SendMoveAsync(socket, result, "Down", 1);
    await Task.Delay(config.MoveIntervalMs + 30);
    await SendMoveAsync(socket, result, "Up", 1);

    for (var sequence = 2; sequence <= config.MovesPerClient; sequence++)
    {
        await Task.Delay(paceMs);
        await SendMoveAsync(socket, result, sequence % 2 == 0 ? "Up" : "Down", sequence);
    }
}

static async Task SendMoveAsync(ClientWebSocket socket, ClientResult result, string direction, long sequence)
{
    var sentAt = Stopwatch.GetTimestamp();
    await SendAsync(socket, new
    {
        type = "move",
        direction,
        sequence,
        clientSentAt = sentAt
    });
    result.SentMoves++;
    result.SentAt[sequence] = sentAt;
}

static async Task ReceiveAsync(ClientWebSocket socket, ClientResult result)
{
    var buffer = new byte[8192];
    while (socket.State == WebSocketState.Open)
    {
        WebSocketReceiveResult received;
        try
        {
            received = await socket.ReceiveAsync(buffer, CancellationToken.None);
        }
        catch
        {
            break;
        }

        if (received.MessageType == WebSocketMessageType.Close)
        {
            break;
        }

        var text = Encoding.UTF8.GetString(buffer, 0, received.Count);
        using var doc = JsonDocument.Parse(text);
        var type = doc.RootElement.GetProperty("type").GetString();
        var payload = doc.RootElement.GetProperty("payload");
        switch (type)
        {
            case "joined":
                result.PlayerId = payload.GetProperty("playerId").GetString();
                result.Position = ReadPosition(payload.GetProperty("position"));
                break;
            case "player_moved":
                if (IsOwnMove(payload, result))
                {
                    result.AcceptedMoves++;
                    TrackRtt(payload, result);
                }
                break;
            case "move_rejected":
                result.RejectedMoves++;
                if (payload.TryGetProperty("reason", out var reasonElement))
                {
                    var reason = reasonElement.GetString() ?? "unknown";
                    result.RejectionReasons[reason] = result.RejectionReasons.GetValueOrDefault(reason) + 1;
                }

                TrackRtt(payload, result);
                break;
            case "snapshot":
                result.Snapshots++;
                break;
        }
    }
}

static async Task WaitForJoinAsync(ClientResult result)
{
    var timeoutAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
    while (result.Position is null && DateTimeOffset.UtcNow < timeoutAt)
    {
        await Task.Delay(20);
    }
}

static string? DirectionToward(TestPosition? from, TestPosition to)
{
    if (from is not { } origin) return null;
    var dx = to.X - origin.X;
    var dy = to.Y - origin.Y;
    if (Math.Abs(dx) + Math.Abs(dy) != 1) return null;
    if (dx > 0) return "Right";
    if (dx < 0) return "Left";
    if (dy > 0) return "Down";
    return "Up";
}

static TestPosition ReadPosition(JsonElement payload)
{
    return new TestPosition(
        payload.GetProperty("x").GetInt32(),
        payload.GetProperty("y").GetInt32());
}

static bool IsOwnMove(JsonElement payload, ClientResult result)
{
    if (string.IsNullOrWhiteSpace(result.PlayerId)) return false;
    if (!payload.TryGetProperty("playerId", out var playerIdElement)) return false;
    return string.Equals(playerIdElement.GetString(), result.PlayerId, StringComparison.Ordinal);
}

static void TrackRtt(JsonElement payload, ClientResult result)
{
    if (!payload.TryGetProperty("sequence", out var sequenceElement)) return;
    var sequence = sequenceElement.GetInt64();
    if (!result.SentAt.TryGetValue(sequence, out var sentAt)) return;

    var elapsed = Stopwatch.GetElapsedTime(sentAt);
    result.RoundTripTimes.Add(elapsed);
}

static async Task SendAsync(ClientWebSocket socket, object payload)
{
    var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
}

static string GetProfile(LoadScenario scenario, int clientIndex)
{
    return scenario switch
    {
        LoadScenario.Collision when clientIndex == 0 => "anchor",
        LoadScenario.Collision => "collider",
        LoadScenario.SyncPace when clientIndex == 0 => "fast",
        LoadScenario.SyncPace when clientIndex == 2 => "slow",
        LoadScenario.SyncPace => "normal",
        _ => "default"
    };
}

sealed class LoadTestConfig
{
    public Uri HttpBaseUri { get; init; } = new("http://localhost:5199");
    public LoadScenario Scenario { get; init; } = LoadScenario.Default;
    public int Rooms { get; init; } = 10;
    public int ClientsPerRoom { get; init; } = 4;
    public int MovesPerClient { get; init; } = 30;
    public int MoveIntervalMs { get; init; } = 110;
    public int FastMoveIntervalMs { get; init; } = 40;
    public int SlowMoveIntervalMs { get; init; } = 250;
    public int ArtificialDelayMs { get; init; }
    public string? AdminToken { get; init; }

    public static LoadTestConfig Parse(string[] args)
    {
        var values = args
            .Select(arg => arg.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].TrimStart('-'), parts => parts[1], StringComparer.OrdinalIgnoreCase);

        var scenario = Enum.TryParse<LoadScenario>(NormalizeScenario(values.GetValueOrDefault("scenario", "default")), true, out var parsed)
            ? parsed
            : LoadScenario.Default;

        var defaultRooms = scenario is LoadScenario.Default ? 10 : 1;
        var defaultClients = scenario is LoadScenario.SyncPace ? 3 : 4;
        var defaultMoves = scenario is LoadScenario.Collision ? 1 : 30;

        return new LoadTestConfig
        {
            HttpBaseUri = new Uri(values.GetValueOrDefault("http", "http://localhost:5199")),
            Scenario = scenario,
            Rooms = ReadInt(values, "rooms", defaultRooms),
            ClientsPerRoom = ReadInt(values, "clients", defaultClients),
            MovesPerClient = ReadInt(values, "moves", defaultMoves),
            MoveIntervalMs = ReadInt(values, "moveIntervalMs", 110),
            FastMoveIntervalMs = ReadInt(values, "fastMoveIntervalMs", 40),
            SlowMoveIntervalMs = ReadInt(values, "slowMoveIntervalMs", 250),
            ArtificialDelayMs = ReadInt(values, "delayMs", 0),
            AdminToken = values.GetValueOrDefault("adminToken") ?? Environment.GetEnvironmentVariable("POKEMON2_ADMIN_TOKEN")
        };
    }

    private static string NormalizeScenario(string value)
    {
        return value.Replace("-", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "", StringComparison.OrdinalIgnoreCase);
    }

    private static int ReadInt(Dictionary<string, string> values, string key, int fallback)
    {
        return int.TryParse(values.GetValueOrDefault(key), out var value) ? value : fallback;
    }
}

enum LoadScenario
{
    Default,
    Collision,
    SyncPace
}

sealed class ClientResult
{
    public ClientResult(string profile)
    {
        Profile = profile;
    }

    public string Profile { get; }
    public string? PlayerId { get; set; }
    public TestPosition? Position { get; set; }
    public int SentMoves { get; set; }
    public int AcceptedMoves { get; set; }
    public int RejectedMoves { get; set; }
    public int Snapshots { get; set; }
    public Dictionary<string, int> RejectionReasons { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<long, long> SentAt { get; } = new();
    public List<TimeSpan> RoundTripTimes { get; } = new();
}

sealed record RoomSummary(string RoomId);
sealed record PlayerIdentityResponse(string UserId, string Token, DateTimeOffset IssuedAt);
sealed record JoinRoomResponse(RoomSummary Room, string PlayerName, string WsUrl);

readonly record struct TestPosition(int X, int Y);
