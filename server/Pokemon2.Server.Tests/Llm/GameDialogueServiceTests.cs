using Microsoft.Extensions.Logging.Abstractions;
using Pokemon2.Server.Llm;

namespace Pokemon2.Server.Tests.Llm;

public sealed class GameDialogueServiceTests
{
    [Fact]
    public async Task GenerateChoicesAsync_StripsCodeFenceAndReturnsChoiceArray()
    {
        var client = new StubLlmTextClient("""
```json
{"choices":["조심할게.","뭐가 있는 거야?","넌 안 무서워?"]}
```
""");
        var service = new GameDialogueService(client);

        var choices = await service.GenerateChoicesAsync("앞쪽 길은 위험하니까 조심해.", CancellationToken.None);

        Assert.Equal(new[] { "조심할게.", "뭐가 있는 거야?", "넌 안 무서워?" }, choices);
    }

    [Fact]
    public async Task GenerateReplyAsync_UsesBinnaPromptWhenRequested()
    {
        var client = new StubLlmTextClient("또 만났네!");
        var service = new GameDialogueService(client);

        var reply = await service.GenerateReplyAsync("binna", "안녕", CancellationToken.None);

        Assert.Equal("또 만났네!", reply);
        Assert.Contains("빛나", client.LastSystemPrompt);
        Assert.Equal("안녕", client.LastUserMessage);
    }

    [Fact]
    public async Task OpenAiCompatibleClient_ParsesChatCompletionsResponse()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"choices":[{"message":{"content":"흥, 나보다 먼저 강해질 생각은 하지 마!"}}]}""")
            });
        using var httpClient = new HttpClient(handler);
        var client = new OpenAiCompatibleLlmClient(
            httpClient,
            new LlmOptions
            {
                ApiKey = "test-key",
                ApiUrl = "https://llm.example.test/v1/chat/completions",
                Model = "solar-mini"
            },
            NullLogger<OpenAiCompatibleLlmClient>.Instance);

        var reply = await client.GenerateTextAsync("system", "user", 80, CancellationToken.None);

        Assert.Equal("흥, 나보다 먼저 강해질 생각은 하지 마!", reply);
        Assert.Equal("Bearer", handler.LastAuthorizationScheme);
        Assert.Equal("test-key", handler.LastAuthorizationParameter);
        Assert.Contains(@"""model"":""solar-mini""", handler.LastRequestBody);
        Assert.Contains(@"""role"":""system""", handler.LastRequestBody);
        Assert.Contains(@"""role"":""user""", handler.LastRequestBody);
        Assert.Contains(@"""max_tokens"":80", handler.LastRequestBody);
    }

    private sealed class StubLlmTextClient : ILlmTextClient
    {
        private readonly string _response;

        public StubLlmTextClient(string response)
        {
            _response = response;
        }

        public bool IsConfigured => true;
        public string LastSystemPrompt { get; private set; } = "";
        public string LastUserMessage { get; private set; } = "";

        public Task<string> GenerateTextAsync(string systemPrompt, string userMessage, int maxOutputTokens, CancellationToken cancellationToken)
        {
            LastSystemPrompt = systemPrompt;
            LastUserMessage = userMessage;
            return Task.FromResult(_response);
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public string LastAuthorizationScheme { get; private set; } = "";
        public string LastAuthorizationParameter { get; private set; } = "";
        public string LastRequestBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastAuthorizationScheme = request.Headers.Authorization?.Scheme ?? "";
            LastAuthorizationParameter = request.Headers.Authorization?.Parameter ?? "";
            LastRequestBody = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return _responder(request);
        }
    }
}
