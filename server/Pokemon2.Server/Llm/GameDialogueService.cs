using System.Text.Json;

namespace Pokemon2.Server.Llm;

public sealed class GameDialogueService
{
    private readonly ILlmTextClient _client;

    public GameDialogueService(ILlmTextClient client)
    {
        _client = client;
    }

    public bool IsConfigured => _client.IsConfigured;

    public async Task<string> GenerateReplyAsync(string? character, string? message, CancellationToken cancellationToken)
    {
        var normalizedMessage = NormalizeMessage(message);
        var prompt = ResolveReplyPrompt(character);
        return await _client.GenerateTextAsync(prompt, normalizedMessage, 120, cancellationToken);
    }

    public async Task<string[]> GenerateChoicesAsync(string? message, CancellationToken cancellationToken)
    {
        var normalizedMessage = NormalizeMessage(message);
        var raw = await _client.GenerateTextAsync(ChoicesSystemPrompt, normalizedMessage, 180, cancellationToken);
        return ParseChoices(raw);
    }

    internal static string[] ParseChoices(string raw)
    {
        var cleaned = raw.Replace("```json", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```", "", StringComparison.Ordinal)
            .Trim();

        using var document = JsonDocument.Parse(cleaned);
        if (!document.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("LLM response does not contain a choices array.");
        }

        var values = choices.EnumerateArray()
            .Select(item => item.GetString()?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(3)
            .Cast<string>()
            .ToArray();

        if (values.Length == 0)
        {
            throw new InvalidOperationException("LLM response returned no usable choices.");
        }

        return values;
    }

    private static string NormalizeMessage(string? message)
    {
        var normalized = message?.Trim() ?? "";
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Message is required.", nameof(message));
        }

        return normalized;
    }

    private static string ResolveReplyPrompt(string? character)
    {
        return string.Equals(character?.Trim(), "binna", StringComparison.OrdinalIgnoreCase)
            ? BinnaSystemPrompt
            : RivalSystemPrompt;
    }

    private const string RivalSystemPrompt = """
너는 포켓몬풍 RPG 게임 속 플레이어의 소꿉친구 '리벨'이야.
성격: 밝고 자신감 있고 살짝 경쟁심 있음.
말투: 짧고 직접적. 반말. 항상 1~2문장으로만 대답.
제약:
- 첫 번째 마을과 연구소 주변 이야기만 알고 있음
- 미래 스토리 스포일러 금지
- 현대 인터넷 용어 사용 금지
- 자신이 AI라고 말하면 안 됨
- 게임 세계관 밖 이야기 금지
응답 예시: "흥, 나보다 먼저 강해질 생각은 하지 마!", "앞쪽 길은 조심해. 괜히 겁먹은 건 아니거든!"
""";

    private const string BinnaSystemPrompt = """
너는 포켓몬풍 RPG 게임 속 플레이어의 소꿉친구 '빛나'야.
성격: 다정하고 활발하며 응원해 주는 편이야.
말투: 짧고 자연스러운 반말. 항상 1~2문장으로만 대답.
제약:
- 첫 번째 마을, 박사 연구소, 1번 도로, 나팔꽃마을 초반 구간만 알고 있음
- 미래 스토리 스포일러 금지
- 현대 인터넷 용어 사용 금지
- 자신이 AI라고 말하면 안 됨
- 게임 세계관 밖 이야기 금지
응답 예시: "와, 벌써 여기까지 왔네! 역시 너답다.", "또 만났네! 같이 마을도 둘러볼래?"
""";

    private const string ChoicesSystemPrompt = """
너는 포켓몬풍 RPG 게임의 대화 선택지 생성기야.
플레이어는 막 스타팅 몬스터를 받고 모험을 시작한 초보 트레이너야.
배경: 시작 마을, 박사 연구소, 1번 도로, 나팔꽃마을 초반 구간.

상대 캐릭터의 대사를 받으면, 플레이어가 할 수 있는 자연스러운 짧은 대답 3개를 생성해.
선택지는 반드시 아래 조건을 따라야 해:
- 상대 대사에 직접 반응해야 함. 뜬금없는 주제 전환 금지
- 상대가 질문하면 답하거나 되묻는 선택지를 포함
- 상대가 도발해도 플레이어는 시비 걸거나 공격적으로 말하지 않음
- 플레이어 선택지는 친근함 / 침착함 / 호기심 위주로 생성
- 직전 대사에 없는 정보(체육관, 전설의 몬스터, 먼 미래 지역 등) 추가 금지
- 포켓몬 세계관 줄거리와 연관 (예: 스타팅 몬스터, 모험, 마을, 트레이너 배틀, 박사)
- 각각 다른 감정/태도 (예: 자신감 있는 / 궁금한 / 도전적인)
- 10자 이내, 반말, 게임 캐릭터 말투
- 현대 인터넷 용어 금지

반드시 아래 JSON 형식으로만 응답해. 다른 텍스트 없이:
{"choices":["선택지1","선택지2","선택지3"]}

예시:
{"choices":["내 파트너가 더 강해!","박사님한테 배웠거든.","같이 강해지자!"]}
""";
}
