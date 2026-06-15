using Microsoft.EntityFrameworkCore;
using Pokemon2.Server.Data;
using Pokemon2.Server.Game;
using Pokemon2.Server.Infrastructure;
using Pokemon2.Server.Llm;
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
var llmOptions = LlmOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(llmOptions);
builder.Services.AddHttpClient<ILlmTextClient, OpenAiCompatibleLlmClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddSingleton<LlmRequestLimiter>();
builder.Services.AddSingleton<GameDialogueService>();
builder.Services.AddSingleton<MapCatalog>();
builder.Services.AddSingleton<ServerMetrics>();
builder.Services.AddSingleton(DogStatsdOptions.FromEnvironment(builder.Configuration));
builder.Services.AddHostedService<DatadogMetricsPublisher>();
builder.Services.AddSingleton<PlayerIdentityService>();
builder.Services.AddSingleton<AdminAccessService>();
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

app.MapGet("/api/health", (LlmOptions llmOptions) => Results.Ok(new
{
    service = "pokemon2-online-server",
    status = "ok",
    utc = DateTimeOffset.UtcNow,
    llm = new
    {
        replyConfigured = llmOptions.IsConfigured(LlmOperation.Reply),
        choicesConfigured = llmOptions.IsConfigured(LlmOperation.Choices)
    }
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

app.MapPost("/api/rooms/{roomId}/join", (HttpContext context, RoomManager rooms, PlayerIdentityService identityService, string roomId, JoinRoomRequest request) =>
{
    if (!identityService.TryResolve(context, out var identity))
    {
        return Results.Json(new { message = "Player identity required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var room = rooms.ListRooms().FirstOrDefault(x => string.Equals(x.RoomId, roomId, StringComparison.OrdinalIgnoreCase));
    if (room is null)
    {
        return Results.NotFound(new { message = "Room not found." });
    }

    var playerName = string.IsNullOrWhiteSpace(request.PlayerName) ? "Player" : request.PlayerName.Trim();
    var wsScheme = context.Request.Scheme == "https" ? "wss" : "ws";
    var wsUrl = $"{wsScheme}://{context.Request.Host}/ws/game?roomId={Uri.EscapeDataString(room.RoomId)}&playerName={Uri.EscapeDataString(playerName)}&{PlayerIdentityService.QueryName}={Uri.EscapeDataString(identity.Token)}";
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

app.MapGet("/api/player/identity", (HttpContext context, PlayerIdentityService identityService) =>
{
    if (identityService.TryResolve(context, out var existingIdentity))
    {
        return Results.Ok(new PlayerIdentityResponse(existingIdentity.UserId, existingIdentity.Token, existingIdentity.IssuedAt));
    }

    var issuedIdentity = identityService.Issue();
    return Results.Ok(new PlayerIdentityResponse(issuedIdentity.UserId, issuedIdentity.Token, issuedIdentity.IssuedAt));
});

app.MapPost("/api/llm/reply", async (HttpContext context, GameDialogueService llm, PlayerIdentityService identityService, LlmReplyRequest request, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await llm.GenerateReplyAsync(request.Character, request.Message, ResolveLlmRateLimitKey(context, identityService), cancellationToken);
        return Results.Ok(new LlmReplyResponse(result.Reply, result.Source, result.Status, result.Character, result.Model));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/api/llm/choices", async (HttpContext context, GameDialogueService llm, PlayerIdentityService identityService, LlmChoicesRequest request, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await llm.GenerateChoicesAsync(request.Message, ResolveLlmRateLimitKey(context, identityService), cancellationToken);
        return Results.Ok(new LlmChoicesResponse(result.Choices, result.Source, result.Status, result.Model));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapGet("/api/saves", async (HttpContext context, GameDbContext db, PlayerIdentityService identityService, string? mode, CancellationToken cancellationToken) =>
{
    if (!identityService.TryResolve(context, out var identity))
    {
        return Results.Json(new { message = "Player identity required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var saveMode = string.IsNullOrWhiteSpace(mode) ? "single" : mode.Trim().ToLowerInvariant();
    var saves = await db.PlayerSaves
        .AsNoTracking()
        .Where(save => save.UserId == identity.UserId && save.Mode == saveMode)
        .OrderBy(save => save.SlotNumber)
        .ToListAsync(cancellationToken);

    return Results.Ok(saves.Select(SaveMapper.ToSummary));
});

app.MapGet("/api/saves/{saveId:guid}", async (HttpContext context, GameDbContext db, PlayerIdentityService identityService, Guid saveId, CancellationToken cancellationToken) =>
{
    if (!identityService.TryResolve(context, out var identity))
    {
        return Results.Json(new { message = "Player identity required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var save = await db.PlayerSaves.AsNoTracking().FirstOrDefaultAsync(x => x.Id == saveId && x.UserId == identity.UserId, cancellationToken);
    return save is null ? Results.NotFound(new { message = "Save not found." }) : Results.Ok(SaveMapper.ToDetail(save));
});

app.MapPost("/api/saves", async (HttpContext context, GameDbContext db, PlayerIdentityService identityService, UpsertSaveRequest request, CancellationToken cancellationToken) =>
{
    if (!identityService.TryResolve(context, out var identity))
    {
        return Results.Json(new { message = "Player identity required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (request.SlotNumber is < 1 or > 3)
    {
        return Results.BadRequest(new { message = "SlotNumber must be between 1 and 3." });
    }

    var now = DateTimeOffset.UtcNow;
    var saveMode = string.IsNullOrWhiteSpace(request.Mode) ? "single" : request.Mode.Trim().ToLowerInvariant();
    if (saveMode is not ("single" or "multi")) saveMode = "single";
    var save = await db.PlayerSaves
        .FirstOrDefaultAsync(x => x.UserId == identity.UserId && x.Mode == saveMode && x.SlotNumber == request.SlotNumber, cancellationToken);
    var created = save is null;

    save ??= new PlayerSave
    {
        Id = Guid.NewGuid(),
        UserId = identity.UserId,
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

app.MapPut("/api/saves/{saveId:guid}", async (HttpContext context, GameDbContext db, PlayerIdentityService identityService, Guid saveId, UpsertSaveRequest request, CancellationToken cancellationToken) =>
{
    if (!identityService.TryResolve(context, out var identity))
    {
        return Results.Json(new { message = "Player identity required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (request.SlotNumber is < 1 or > 3)
    {
        return Results.BadRequest(new { message = "SlotNumber must be between 1 and 3." });
    }

    var save = await db.PlayerSaves.FirstOrDefaultAsync(x => x.Id == saveId && x.UserId == identity.UserId, cancellationToken);
    if (save is null)
    {
        return Results.NotFound(new { message = "Save not found." });
    }

    SaveMapper.Apply(save, request, DateTimeOffset.UtcNow);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(SaveMapper.ToDetail(save));
});

app.MapDelete("/api/saves/{saveId:guid}", async (HttpContext context, GameDbContext db, PlayerIdentityService identityService, Guid saveId, CancellationToken cancellationToken) =>
{
    if (!identityService.TryResolve(context, out var identity))
    {
        return Results.Json(new { message = "Player identity required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var deleted = await db.PlayerSaves.Where(x => x.Id == saveId && x.UserId == identity.UserId).ExecuteDeleteAsync(cancellationToken);
    return deleted == 0 ? Results.NotFound(new { message = "Save not found." }) : Results.NoContent();
});

app.MapGet("/api/admin/metrics", (HttpContext context, RoomManager rooms, ServerMetrics metrics, AdminAccessService adminAccess) =>
{
    if (!adminAccess.IsAuthorized(context))
    {
        return Results.Json(new { message = "Admin access required." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    return Results.Ok(new
    {
        rooms = rooms.ListRooms(),
        totals = metrics.GetSnapshot()
    });
});

app.Map("/ws/game", async (HttpContext context, PlayerIdentityService identityService, WebSocketGameEndpoint endpoint) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("WebSocket request required.");
        return;
    }

    if (!identityService.TryResolve(context, out var identity))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Player identity required.");
        return;
    }

    var roomId = context.Request.Query["roomId"].ToString();
    var playerName = context.Request.Query["playerName"].ToString();
    await endpoint.HandleAsync(context, roomId, playerName, identity.UserId);
});

app.Run();

static async Task EnsureDatabaseAsync(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        await BaselinePreMigrationSchemaAsync(db, app.Logger);
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "PostgreSQL database is not ready. Save APIs require a configured database.");
    }
}

static async Task BaselinePreMigrationSchemaAsync(GameDbContext db, ILogger logger)
{
    const string initialMigrationId = "20260615170000_InitialSchema";
    const string productVersion = "10.0.1";

    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();

    try
    {
        var historyExists = await TableExistsAsync(connection, "__EFMigrationsHistory");
        var playerSavesExists = await TableExistsAsync(connection, "player_saves");
        var battleResultsExists = await TableExistsAsync(connection, "battle_results");
        if (historyExists || (!playerSavesExists && !battleResultsExists))
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync($$"""
            CREATE TABLE IF NOT EXISTS player_saves (
                id uuid NOT NULL,
                user_id character varying(80),
                slot_number integer NOT NULL,
                mode character varying(24) NOT NULL,
                player_name character varying(40) NOT NULL,
                current_map character varying(64) NOT NULL,
                position_x integer NOT NULL,
                position_y integer NOT NULL,
                starter_id character varying(32),
                starter_name character varying(40),
                starter_level integer,
                starter_current_hp integer,
                play_time_seconds bigint NOT NULL DEFAULT 0,
                events_json jsonb NOT NULL DEFAULT '{}'::jsonb,
                game_state_json jsonb NOT NULL DEFAULT '{}'::jsonb,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                updated_at timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT "PK_player_saves" PRIMARY KEY (id)
            );
            ALTER TABLE player_saves ADD COLUMN IF NOT EXISTS user_id character varying(80);
            ALTER TABLE player_saves ADD COLUMN IF NOT EXISTS starter_id character varying(32);
            ALTER TABLE player_saves ADD COLUMN IF NOT EXISTS starter_name character varying(40);
            ALTER TABLE player_saves ADD COLUMN IF NOT EXISTS starter_level integer;
            ALTER TABLE player_saves ADD COLUMN IF NOT EXISTS starter_current_hp integer;
            ALTER TABLE player_saves ADD COLUMN IF NOT EXISTS play_time_seconds bigint NOT NULL DEFAULT 0;
            ALTER TABLE player_saves ADD COLUMN IF NOT EXISTS events_json jsonb NOT NULL DEFAULT '{}'::jsonb;
            ALTER TABLE player_saves ADD COLUMN IF NOT EXISTS game_state_json jsonb NOT NULL DEFAULT '{}'::jsonb;
            ALTER TABLE player_saves ADD COLUMN IF NOT EXISTS created_at timestamp with time zone NOT NULL DEFAULT NOW();
            ALTER TABLE player_saves ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone NOT NULL DEFAULT NOW();
            UPDATE player_saves
            SET user_id = 'legacy-default'
            WHERE user_id IS NULL OR btrim(user_id) = '';
            ALTER TABLE player_saves ALTER COLUMN user_id SET NOT NULL;
            DROP INDEX IF EXISTS "IX_player_saves_Mode_SlotNumber";
            DROP INDEX IF EXISTS ix_player_saves_mode_slot_number;
            CREATE UNIQUE INDEX IF NOT EXISTS ix_player_saves_user_mode_slot_number
            ON player_saves (user_id, mode, slot_number);

            CREATE TABLE IF NOT EXISTS battle_results (
                id uuid NOT NULL,
                room_id character varying(40) NOT NULL,
                player_id character varying(64) NOT NULL,
                player_name character varying(40) NOT NULL,
                monster_id character varying(64) NOT NULL,
                monster_name character varying(40) NOT NULL,
                won boolean NOT NULL,
                server_tick bigint NOT NULL,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT "PK_battle_results" PRIMARY KEY (id)
            );
            CREATE INDEX IF NOT EXISTS ix_battle_results_room_id_created_at
            ON battle_results (room_id, created_at);

            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('{{initialMigrationId}}', '{{productVersion}}')
            ON CONFLICT ("MigrationId") DO NOTHING;
            """);

        logger.LogInformation("Baselined existing PostgreSQL schema to migration {MigrationId}.", initialMigrationId);
    }
    finally
    {
        await connection.CloseAsync();
    }
}

static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection connection, string tableName)
{
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT to_regclass(@tableName) IS NOT NULL;";
    var parameter = command.CreateParameter();
    parameter.ParameterName = "tableName";
    parameter.Value = tableName;
    command.Parameters.Add(parameter);
    var result = await command.ExecuteScalarAsync();
    return result is true || result is bool boolean && boolean;
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

static string ResolveLlmRateLimitKey(HttpContext context, PlayerIdentityService identityService)
{
    if (identityService.TryResolve(context, out var identity))
    {
        return identity.UserId;
    }

    var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
    if (!string.IsNullOrWhiteSpace(forwardedFor))
    {
        return forwardedFor.Split(',')[0].Trim();
    }

    return context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
}

public sealed record CreateRoomRequest(string? RoomName, string? MapId);
public sealed record JoinRoomRequest(string? PlayerName);
