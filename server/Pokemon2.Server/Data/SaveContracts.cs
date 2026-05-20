using System.Text.Json;

namespace Pokemon2.Server.Data;

public sealed record SaveStarterDto(
    string? Id,
    string? Name,
    int? Level,
    int? CurrentHp);

public sealed record UpsertSaveRequest(
    int SlotNumber,
    string? Mode,
    string? PlayerName,
    string? CurrentMap,
    int PositionX,
    int PositionY,
    SaveStarterDto? Starter,
    JsonElement? Events,
    JsonElement? GameState,
    long PlayTimeSeconds);

public sealed record SaveSummaryResponse(
    Guid Id,
    int SlotNumber,
    string Mode,
    string PlayerName,
    string CurrentMap,
    int PositionX,
    int PositionY,
    SaveStarterDto? Starter,
    long PlayTimeSeconds,
    DateTimeOffset UpdatedAt);

public sealed record SaveDetailResponse(
    Guid Id,
    int SlotNumber,
    string Mode,
    string PlayerName,
    string CurrentMap,
    int PositionX,
    int PositionY,
    SaveStarterDto? Starter,
    JsonElement Events,
    JsonElement GameState,
    long PlayTimeSeconds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
