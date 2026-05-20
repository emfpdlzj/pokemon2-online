using Microsoft.EntityFrameworkCore;
using Pokemon2.Server.Data;
using Pokemon2.Server.Game;
using Pokemon2.Server.Infrastructure;
using Pokemon2.Server.Networking;

DotEnv.LoadForServer();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddDbContext<GameDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? Environment.GetEnvironmentVariable("POKEMON2_DATABASE_URL")
        ?? throw new InvalidOperationException("Database connection is not configured. Set ConnectionStrings__DefaultConnection or POKEMON2_DATABASE_URL in .env or server/.env.");

    options.UseNpgsql(NormalizePostgresConnectionString(connectionString));
});
builder.Services.AddSingleton<MapCatalog>();
builder.Services.AddSingleton<ServerMetrics>();
builder.Services.AddSingleton<IBattleResultStore, EfBattleResultStore>();
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<WebSocketGameEndpoint>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(20)
});

await EnsureDatabaseAsync(app);

app.MapGet("/api/health", () => Results.Ok(new
{
    service = "pokemon2-online-server",
    status = "ok",
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/rooms", (RoomManager rooms) =>
{
    return Results.Ok(rooms.ListRooms());
});

app.MapPost("/api/rooms", (RoomManager rooms, CreateRoomRequest request) =>
{
    var room = rooms.CreateRoom(request.RoomName, request.MapId);
    return Results.Created($"/api/rooms/{room.RoomId}", room);
});

app.MapPost("/api/rooms/{roomId}/join", (HttpContext context, RoomManager rooms, string roomId, JoinRoomRequest request) =>
{
    var room = rooms.ListRooms().FirstOrDefault(x => string.Equals(x.RoomId, roomId, StringComparison.OrdinalIgnoreCase));
    if (room is null)
    {
        return Results.NotFound(new { message = "Room not found." });
    }

    var playerName = string.IsNullOrWhiteSpace(request.PlayerName) ? "Player" : request.PlayerName.Trim();
    var wsScheme = context.Request.Scheme == "https" ? "wss" : "ws";
    var wsUrl = $"{wsScheme}://{context.Request.Host}/ws/game?roomId={Uri.EscapeDataString(room.RoomId)}&playerName={Uri.EscapeDataString(playerName)}";
    return Results.Ok(new { room, playerName, wsUrl });
});

app.MapPost("/api/sessions/single", () =>
{
    return Results.Created("/api/sessions/single", new
    {
        sessionId = $"single-{Guid.NewGuid():N}"[..20],
        mode = "single",
        serverTime = DateTimeOffset.UtcNow
    });
});

app.MapGet("/api/saves", async (GameDbContext db, string? mode, CancellationToken cancellationToken) =>
{
    var saveMode = string.IsNullOrWhiteSpace(mode) ? "single" : mode.Trim().ToLowerInvariant();
    var saves = await db.PlayerSaves
        .AsNoTracking()
        .Where(save => save.Mode == saveMode)
        .OrderBy(save => save.SlotNumber)
        .ToListAsync(cancellationToken);

    return Results.Ok(saves.Select(SaveMapper.ToSummary));
});

app.MapGet("/api/saves/{saveId:guid}", async (GameDbContext db, Guid saveId, CancellationToken cancellationToken) =>
{
    var save = await db.PlayerSaves.AsNoTracking().FirstOrDefaultAsync(x => x.Id == saveId, cancellationToken);
    return save is null ? Results.NotFound(new { message = "Save not found." }) : Results.Ok(SaveMapper.ToDetail(save));
});

app.MapPost("/api/saves", async (GameDbContext db, UpsertSaveRequest request, CancellationToken cancellationToken) =>
{
    if (request.SlotNumber is < 1 or > 3)
    {
        return Results.BadRequest(new { message = "SlotNumber must be between 1 and 3." });
    }

    var now = DateTimeOffset.UtcNow;
    var saveMode = string.IsNullOrWhiteSpace(request.Mode) ? "single" : request.Mode.Trim().ToLowerInvariant();
    if (saveMode is not ("single" or "multi")) saveMode = "single";
    var save = await db.PlayerSaves
        .FirstOrDefaultAsync(x => x.Mode == saveMode && x.SlotNumber == request.SlotNumber, cancellationToken);
    var created = save is null;

    save ??= new PlayerSave
    {
        Id = Guid.NewGuid(),
        CreatedAt = now
    };

    SaveMapper.Apply(save, request, now);

    if (created)
    {
        db.PlayerSaves.Add(save);
    }

    await db.SaveChangesAsync(cancellationToken);

    return created
        ? Results.Created($"/api/saves/{save.Id}", SaveMapper.ToDetail(save))
        : Results.Ok(SaveMapper.ToDetail(save));
});

app.MapPut("/api/saves/{saveId:guid}", async (GameDbContext db, Guid saveId, UpsertSaveRequest request, CancellationToken cancellationToken) =>
{
    if (request.SlotNumber is < 1 or > 3)
    {
        return Results.BadRequest(new { message = "SlotNumber must be between 1 and 3." });
    }

    var save = await db.PlayerSaves.FirstOrDefaultAsync(x => x.Id == saveId, cancellationToken);
    if (save is null)
    {
        return Results.NotFound(new { message = "Save not found." });
    }

    SaveMapper.Apply(save, request, DateTimeOffset.UtcNow);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(SaveMapper.ToDetail(save));
});

app.MapDelete("/api/saves/{saveId:guid}", async (GameDbContext db, Guid saveId, CancellationToken cancellationToken) =>
{
    var deleted = await db.PlayerSaves.Where(x => x.Id == saveId).ExecuteDeleteAsync(cancellationToken);
    return deleted == 0 ? Results.NotFound(new { message = "Save not found." }) : Results.NoContent();
});

app.MapGet("/api/admin/metrics", (RoomManager rooms, ServerMetrics metrics) =>
{
    return Results.Ok(new
    {
        rooms = rooms.ListRooms(),
        totals = metrics.GetSnapshot()
    });
});

app.Map("/ws/game", async (HttpContext context, WebSocketGameEndpoint endpoint) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("WebSocket request required.");
        return;
    }

    var roomId = context.Request.Query["roomId"].ToString();
    var playerName = context.Request.Query["playerName"].ToString();
    await endpoint.HandleAsync(context, roomId, playerName);
});

app.Run();

static async Task EnsureDatabaseAsync(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "PostgreSQL database is not ready. Save APIs require a configured database.");
    }
}

static string NormalizePostgresConnectionString(string connectionString)
{
    if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) ||
        (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
    {
        return connectionString;
    }

    var userInfo = uri.UserInfo.Split(':', 2);
    var username = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? "");
    var password = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? "");
    var database = uri.AbsolutePath.TrimStart('/');
    return $"Host={uri.Host};Port={uri.Port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
}

public sealed record CreateRoomRequest(string? RoomName, string? MapId);
public sealed record JoinRoomRequest(string? PlayerName);
