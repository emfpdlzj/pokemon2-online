using Microsoft.Extensions.Logging.Abstractions;
using Pokemon2.Server.Infrastructure;
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
        var service = CreateService(client);

        var result = await service.GenerateChoicesAsync("앞쪽 길은 위험하니까 조심해.", "user-1", CancellationToken.None);

        Assert.Equal(new[] { "조심할게.", "뭐가 있는 거야?", "넌 안 무서워?" }, result.Choices);
        Assert.Equal("llm", result.Source);
    }

    [Fact]
    public async Task GenerateReplyAsync_UsesBinnaPromptWhenRequested()
    {
        var client = new StubLlmTextClient("또 만났네!");
        var service = CreateService(client);

        var reply = await service.GenerateReplyAsync("binna", "안녕", "user-1", CancellationToken.None);

        Assert.Equal("또 만났네!", reply.Reply);
        Assert.Contains("빛나", client.LastSystemPrompt);
        Assert.Equal("안녕", client.LastUserMessage);
    }

    [Fact]
    public async Task GenerateReplyAsync_FallsBackWhenProviderFails()
    {
        var client = new ThrowingLlmTextClient(new HttpRequestException("provider down"));
        var service = CreateService(client);

        var reply = await service.GenerateReplyAsync("rival", "안녕", "user-1", CancellationToken.None);

        Assert.Equal("fallback", reply.Source);
        Assert.Equal("provider_error", reply.Status);
        Assert.Equal("나중에 다시 말 걸어줘!", reply.Reply);
    }

    [Fact]
    public async Task GenerateReplyAsync_FallsBackWhenReplyContainsUnsupportedCharacters()
    {
        var client = new StubLlmTextClient("⚠️ 시스템 메시지");
        var service = CreateService(client);

        var reply = await service.GenerateReplyAsync("rival", "안녕", "user-1", CancellationToken.None);

        Assert.Equal("fallback", reply.Source);
        Assert.Equal("invalid_response", reply.Status);
        Assert.Equal("나중에 다시 말 걸어줘!", reply.Reply);
    }

    [Fact]
    public async Task GenerateChoicesAsync_FallsBackWhenChoicesPayloadIsMalformed()
    {
        var client = new StubLlmTextClient("""{"items":["하나"]}""");
        var service = CreateService(client);

        var result = await service.GenerateChoicesAsync("안녕", "user-1", CancellationToken.None);

        Assert.Equal("fallback", result.Source);
        Assert.Equal("invalid_response", result.Status);
        Assert.Equal(3, result.Choices.Length);
    }

    [Fact]
    public async Task OpenAiCompatibleClient_ParsesChatCompletionsResponseAndUsage()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"model":"solar-mini","choices":[{"message":{"content":"흥, 나보다 먼저 강해질 생각은 하지 마!"}}],"usage":{"prompt_tokens":12,"completion_tokens":7,"total_tokens":19}}""")
            });
        using var httpClient = new HttpClient(handler);
        var client = new OpenAiCompatibleLlmClient(
            httpClient,
            new LlmOptions
            {
                Reply = new LlmEndpointOptions
                {
                    ApiKey = "test-key",
                    ApiUrl = "https://llm.example.test/v1/chat/completions",
                    Model = "solar-mini",
                    MaxOutputTokens = 80,
                    PromptCostPer1KUsd = 0.003m,
                    CompletionCostPer1KUsd = 0.006m
                }
            },
            NullLogger<OpenAiCompatibleLlmClient>.Instance);

        var reply = await client.GenerateTextAsync(new LlmCompletionRequest(LlmOperation.Reply, "system", "user"), CancellationToken.None);

        Assert.Equal("흥, 나보다 먼저 강해질 생각은 하지 마!", reply.Text);
        Assert.Equal("Bearer", handler.LastAuthorizationScheme);
        Assert.Equal("test-key", handler.LastAuthorizationParameter);
        Assert.Contains(@"""model"":""solar-mini""", handler.LastRequestBody);
        Assert.Contains(@"""role"":""system""", handler.LastRequestBody);
        Assert.Contains(@"""role"":""user""", handler.LastRequestBody);
        Assert.Contains(@"""max_tokens"":80", handler.LastRequestBody);
        Assert.Equal(12, reply.Usage.PromptTokens);
        Assert.Equal(7, reply.Usage.CompletionTokens);
        Assert.Equal(19, reply.Usage.TotalTokens);
        Assert.Equal(0.000078m, reply.Usage.EstimatedCostUsd);
    }

    private sealed class StubLlmTextClient : ILlmTextClient
    {
        private readonly string _response;

        public StubLlmTextClient(string response)
        {
            _response = response;
        }

        public bool IsConfigured(LlmOperation operation) => true;
        public string LastSystemPrompt { get; private set; } = "";
        public string LastUserMessage { get; private set; } = "";

        public Task<LlmCompletionResult> GenerateTextAsync(LlmCompletionRequest request, CancellationToken cancellationToken)
        {
            LastSystemPrompt = request.SystemPrompt;
            LastUserMessage = request.UserMessage;
            return Task.FromResult(new LlmCompletionResult(
                _response,
                "test-model",
                new LlmCompletionUsage(10, 5, 15, 0.00005m)));
        }
    }

    private sealed class ThrowingLlmTextClient : ILlmTextClient
    {
        private readonly Exception _exception;

        public ThrowingLlmTextClient(Exception exception)
        {
            _exception = exception;
        }

        public bool IsConfigured(LlmOperation operation) => true;

        public Task<LlmCompletionResult> GenerateTextAsync(LlmCompletionRequest request, CancellationToken cancellationToken)
        {
            return Task.FromException<LlmCompletionResult>(_exception);
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

    private static GameDialogueService CreateService(ILlmTextClient client)
    {
        return new GameDialogueService(
            client,
            new LlmOptions
            {
                Reply = new LlmEndpointOptions
                {
                    ApiKey = "key",
                    ApiUrl = "https://llm.example.test/reply",
                    Model = "reply-model",
                    MaxOutputTokens = 120
                },
                Choices = new LlmEndpointOptions
                {
                    ApiKey = "key",
                    ApiUrl = "https://llm.example.test/choices",
                    Model = "choices-model",
                    MaxOutputTokens = 180
                },
                RateLimitPerMinute = 5
            },
            new LlmRequestLimiter(new LlmOptions { RateLimitPerMinute = 5 }),
            new ServerMetrics(),
            NullLogger<GameDialogueService>.Instance);
    }
}
