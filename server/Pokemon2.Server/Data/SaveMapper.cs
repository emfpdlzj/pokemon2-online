using System.Text.Json;

namespace Pokemon2.Server.Data;

public static class SaveMapper
{
    public static SaveSummaryResponse ToSummary(PlayerSave save)
    {
        return new SaveSummaryResponse(
            save.Id,
            save.SlotNumber,
            save.Mode,
            save.PlayerName,
            save.CurrentMap,
            save.PositionX,
            save.PositionY,
            ToStarter(save),
            save.PlayTimeSeconds,
            save.UpdatedAt);
    }

    public static SaveDetailResponse ToDetail(PlayerSave save)
    {
        return new SaveDetailResponse(
            save.Id,
            save.SlotNumber,
            save.Mode,
            save.PlayerName,
            save.CurrentMap,
            save.PositionX,
            save.PositionY,
            ToStarter(save),
            ParseJson(save.EventsJson),
            ParseJson(save.GameStateJson),
            save.PlayTimeSeconds,
            save.CreatedAt,
            save.UpdatedAt);
    }

    public static void Apply(PlayerSave save, UpsertSaveRequest request, DateTimeOffset now)
    {
        save.SlotNumber = Math.Clamp(request.SlotNumber, 1, 3);
        save.Mode = NormalizeMode(request.Mode);
        save.PlayerName = NormalizeText(request.PlayerName, "주인공", 40);
        save.CurrentMap = NormalizeText(request.CurrentMap, "hometown", 64);
        save.PositionX = request.PositionX;
        save.PositionY = request.PositionY;
        save.StarterId = NormalizeNullableText(request.Starter?.Id, 32);
        save.StarterName = NormalizeNullableText(request.Starter?.Name, 40);
        save.StarterLevel = request.Starter?.Level;
        save.StarterCurrentHp = request.Starter?.CurrentHp;
        save.EventsJson = ToRawJson(request.Events, "{}");
        save.GameStateJson = ToRawJson(request.GameState, "{}");
        save.PlayTimeSeconds = Math.Max(0, request.PlayTimeSeconds);
        save.UpdatedAt = now;
    }

    private static SaveStarterDto? ToStarter(PlayerSave save)
    {
        if (string.IsNullOrWhiteSpace(save.StarterId) && string.IsNullOrWhiteSpace(save.StarterName))
        {
            return null;
        }

        return new SaveStarterDto(save.StarterId, save.StarterName, save.StarterLevel, save.StarterCurrentHp);
    }

    private static string NormalizeMode(string? mode)
    {
        var normalized = NormalizeText(mode, "single", 24).ToLowerInvariant();
        return normalized is "single" or "multi" ? normalized : "single";
    }

    private static string NormalizeText(string? value, string fallback, int maxLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static string? NormalizeNullableText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static string ToRawJson(JsonElement? value, string fallback)
    {
        return value.HasValue ? value.Value.GetRawText() : fallback;
    }

    private static JsonElement ParseJson(string json)
    {
        return JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(json) ? "{}" : json);
    }
}
