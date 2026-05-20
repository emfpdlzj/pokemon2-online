using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var config = LoadTestConfig.Parse(args);
using var http = new HttpClient { BaseAddress = config.HttpBaseUri };

Console.WriteLine($"target={config.HttpBaseUri} rooms={config.Rooms} clientsPerRoom={config.ClientsPerRoom} moves={config.MovesPerClient} delayMs={config.ArtificialDelayMs}");

var roomIds = new List<string>();
for (var i = 0; i < config.Rooms; i++)
{
    var response = await http.PostAsJsonAsync("/api/rooms", new { roomName = $"load-{i + 1}", mapId = "hometown" });
    response.EnsureSuccessStatusCode();
    var room = await response.Content.ReadFromJsonAsync<RoomSummary>();
    roomIds.Add(room?.RoomId ?? throw new InvalidOperationException("Room creation response missing roomId."));
}

var stopwatch = Stopwatch.StartNew();
var tasks = roomIds.SelectMany((roomId, roomIndex) =>
    Enumerable.Range(0, config.ClientsPerRoom)
        .Select(clientIndex => RunClientAsync(config, roomId, roomIndex, clientIndex))).ToArray();

var results = await Task.WhenAll(tasks);
stopwatch.Stop();

var accepted = results.Sum(result => result.AcceptedMoves);
var rejected = results.Sum(result => result.RejectedMoves);
var snapshots = results.Sum(result => result.Snapshots);
var avgRtt = results.SelectMany(result => result.RoundTripTimes).DefaultIfEmpty(TimeSpan.Zero).Average(span => span.TotalMilliseconds);

Console.WriteLine("---- result ----");
Console.WriteLine($"elapsedMs={stopwatch.ElapsedMilliseconds}");
Console.WriteLine($"acceptedMoves={accepted}");
Console.WriteLine($"rejectedMoves={rejected}");
Console.WriteLine($"snapshots={snapshots}");
Console.WriteLine($"avgObservedRttMs={Math.Round(avgRtt, 2)}");

var metrics = await http.GetFromJsonAsync<JsonElement>("/api/admin/metrics");
Console.WriteLine("---- server metrics ----");
Console.WriteLine(JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));

static async Task<ClientResult> RunClientAsync(LoadTestConfig config, string roomId, int roomIndex, int clientIndex)
{
    using var socket = new ClientWebSocket();
    var uri = new Uri(config.WebSocketBaseUri, $"/ws/game?roomId={roomId}&playerName=bot-{roomIndex}-{clientIndex}");
    await socket.ConnectAsync(uri, CancellationToken.None);

    var result = new ClientResult();
    var receiveTask = Task.Run(() => ReceiveAsync(socket, result));

    var directions = new[] { "Left", "Right", "Up", "Down" };
    for (var sequence = 1; sequence <= config.MovesPerClient; sequence++)
    {
        if (config.ArtificialDelayMs > 0)
        {
            await Task.Delay(config.ArtificialDelayMs);
        }

        var direction = directions[(sequence + clientIndex) % directions.Length];
        var sentAt = Stopwatch.GetTimestamp();
        await SendAsync(socket, new
        {
            type = "move",
            direction,
            sequence,
            clientSentAt = sentAt
        });
        result.SentAt[sequence] = sentAt;
        await Task.Delay(config.MoveIntervalMs);
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
        switch (type)
        {
            case "player_moved":
                result.AcceptedMoves++;
                TrackRtt(doc.RootElement.GetProperty("payload"), result);
                break;
            case "move_rejected":
                result.RejectedMoves++;
                TrackRtt(doc.RootElement.GetProperty("payload"), result);
                break;
            case "snapshot":
                result.Snapshots++;
                break;
        }
    }
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

sealed class LoadTestConfig
{
    public Uri HttpBaseUri { get; init; } = new("http://localhost:5000");
    public Uri WebSocketBaseUri { get; init; } = new("ws://localhost:5000");
    public int Rooms { get; init; } = 10;
    public int ClientsPerRoom { get; init; } = 4;
    public int MovesPerClient { get; init; } = 30;
    public int MoveIntervalMs { get; init; } = 110;
    public int ArtificialDelayMs { get; init; }

    public static LoadTestConfig Parse(string[] args)
    {
        var values = args
            .Select(arg => arg.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].TrimStart('-'), parts => parts[1], StringComparer.OrdinalIgnoreCase);

        return new LoadTestConfig
        {
            HttpBaseUri = new Uri(values.GetValueOrDefault("http", "http://localhost:5000")),
            WebSocketBaseUri = new Uri(values.GetValueOrDefault("ws", "ws://localhost:5000")),
            Rooms = ReadInt(values, "rooms", 10),
            ClientsPerRoom = ReadInt(values, "clients", 4),
            MovesPerClient = ReadInt(values, "moves", 30),
            MoveIntervalMs = ReadInt(values, "moveIntervalMs", 110),
            ArtificialDelayMs = ReadInt(values, "delayMs", 0)
        };
    }

    private static int ReadInt(Dictionary<string, string> values, string key, int fallback)
    {
        return int.TryParse(values.GetValueOrDefault(key), out var value) ? value : fallback;
    }
}

sealed class ClientResult
{
    public int AcceptedMoves { get; set; }
    public int RejectedMoves { get; set; }
    public int Snapshots { get; set; }
    public Dictionary<long, long> SentAt { get; } = new();
    public List<TimeSpan> RoundTripTimes { get; } = new();
}

sealed record RoomSummary(string RoomId);
