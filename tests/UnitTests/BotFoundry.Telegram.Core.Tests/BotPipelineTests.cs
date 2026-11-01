using BotFoundry.Telegram.Abstractions.Models;
using BotFoundry.Telegram.Abstractions.Ports;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotFoundry.Telegram.Core.Tests;

public sealed class BotPipelineTests
{
    [Fact]
    public async Task ProcessAsync_AuthDenies_StopsImmediately()
    {
        var auth = new StubAuth(false);
        var handler = new SpyTopicHandler("start", FlowResult.Complete());
        var step = new SpyConversationStep("step1");
        var llm = new SpyLlmBridge("reply");
        var store = new SpySessionStore();
        var pipeline = CreatePipeline(store, [handler], [step], auth, llm);
        var context = CreateContext("/start");

        await pipeline.ProcessAsync(context);

        auth.CallCount.Should().Be(1);
        handler.CallCount.Should().Be(0);
        step.ValidateCallCount.Should().Be(0);
        llm.CallCount.Should().Be(0);
        store.SaveCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_AuthDenies_DoesNotPersistSession()
    {
        var store = new SpySessionStore();
        var session = CreateSession();
        var pipeline = CreatePipeline(store, auth: new StubAuth(false));
        var context = CreateContext("/start", session);

        await pipeline.ProcessAsync(context);

        store.SavedSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_AuthAllows_InvokesHandler()
    {
        var handler = new SpyTopicHandler("start", FlowResult.Complete());
        var pipeline = CreatePipeline(new SpySessionStore(), [handler], auth: new StubAuth(true));
        var context = CreateContext("/start");

        await pipeline.ProcessAsync(context);

        handler.CallCount.Should().Be(1);
        handler.LastContext.Should().BeSameAs(context);
    }

    [Fact]
    public async Task ProcessAsync_ActiveFlow_DelegatesToStepRunner()
    {
        var step = new SpyConversationStep("step1") { ValidateResult = true, ProcessResult = StepResult.Continue() };
        var session = CreateSession(activeTopic: "flow", activeStepId: "step1");
        var pipeline = CreatePipeline(new SpySessionStore(), steps: [step]);

        await pipeline.ProcessAsync(CreateContext("answer", session));

        step.ValidateCallCount.Should().Be(1);
        step.ProcessCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessAsync_ActiveFlowWithCommand_DoesNotRouteCommand()
    {
        var handler = new SpyTopicHandler("start", FlowResult.Complete());
        var step = new SpyConversationStep("step1") { ValidateResult = true, ProcessResult = StepResult.Continue() };
        var session = CreateSession(activeTopic: "flow", activeStepId: "step1");
        var pipeline = CreatePipeline(new SpySessionStore(), [handler], [step]);

        await pipeline.ProcessAsync(CreateContext("/start", session));

        step.ValidateCallCount.Should().Be(1);
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_CommandText_RoutesToHandler()
    {
        var handler = new SpyTopicHandler("start", FlowResult.Complete());
        var pipeline = CreatePipeline(new SpySessionStore(), [handler]);

        await pipeline.ProcessAsync(CreateContext("/start"));

        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessAsync_FreeTextWithoutLlm_IsSilent()
    {
        var sender = new SpyMessageSender();
        var pipeline = CreatePipeline(new SpySessionStore());

        await pipeline.ProcessAsync(CreateContext("hello", sender: sender));

        sender.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_FreeTextWithLlm_SendsLlmReply()
    {
        var sender = new SpyMessageSender();
        var llm = new SpyLlmBridge("LLM reply");
        var pipeline = CreatePipeline(new SpySessionStore(), llm: llm);
        var context = CreateContext("hello", sender: sender);

        await pipeline.ProcessAsync(context);

        llm.CallCount.Should().Be(1);
        llm.LastMessage.Should().Be("hello");
        sender.Messages.Should().ContainSingle().Which.Should().Be((123L, "LLM reply"));
    }

    [Fact]
    public async Task ProcessAsync_HandlerThrows_StillPersistsSession()
    {
        var store = new SpySessionStore();
        var handler = new SpyTopicHandler("start", FlowResult.Complete()) { ExceptionToThrow = new InvalidOperationException("boom") };
        var session = CreateSession();
        var pipeline = CreatePipeline(store, [handler]);

        var act = () => pipeline.ProcessAsync(CreateContext("/start", session));

        await act.Should().ThrowAsync<InvalidOperationException>();
        store.SavedSessions.Should().ContainSingle().Which.Should().BeSameAs(session);
    }

    [Fact]
    public async Task ProcessAsync_StepRunnerThrows_StillPersistsSession()
    {
        var store = new SpySessionStore();
        var step = new SpyConversationStep("step1") { ExceptionToThrowOnValidate = new InvalidOperationException("boom") };
        var session = CreateSession(activeTopic: "flow", activeStepId: "step1");
        var pipeline = CreatePipeline(store, steps: [step]);

        var act = () => pipeline.ProcessAsync(CreateContext("answer", session));

        await act.Should().ThrowAsync<InvalidOperationException>();
        store.SavedSessions.Should().ContainSingle().Which.Should().BeSameAs(session);
    }

    [Fact]
    public async Task ProcessAsync_LlmThrows_StillPersistsSession()
    {
        var store = new SpySessionStore();
        var llm = new SpyLlmBridge("reply") { ExceptionToThrow = new InvalidOperationException("boom") };
        var session = CreateSession();
        var pipeline = CreatePipeline(store, llm: llm);

        var act = () => pipeline.ProcessAsync(CreateContext("hello", session));

        await act.Should().ThrowAsync<InvalidOperationException>();
        store.SavedSessions.Should().ContainSingle().Which.Should().BeSameAs(session);
    }

    [Fact]
    public async Task ProcessAsync_AuthThrows_StillPersistsSession()
    {
        var store = new SpySessionStore();
        var auth = new StubAuth(true) { ExceptionToThrow = new InvalidOperationException("boom") };
        var session = CreateSession();
        var pipeline = CreatePipeline(store, auth: auth);

        var act = () => pipeline.ProcessAsync(CreateContext("/start", session));

        await act.Should().ThrowAsync<InvalidOperationException>();
        store.SavedSessions.Should().ContainSingle().Which.Should().BeSameAs(session);
    }

    [Fact]
    public async Task ProcessAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var auth = new StubAuth(true);
        var handler = new SpyTopicHandler("start", FlowResult.Complete());
        var store = new SpySessionStore();
        var pipeline = CreatePipeline(store, [handler], auth: auth);

        await pipeline.ProcessAsync(CreateContext("/start"), cts.Token);

        auth.LastCancellationToken.Should().Be(cts.Token);
        handler.LastCancellationToken.Should().Be(cts.Token);
        store.LastCancellationToken.Should().Be(cts.Token);
    }

    private static BotPipeline CreatePipeline(
        SpySessionStore store,
        IEnumerable<ITopicHandler>? handlers = null,
        IEnumerable<IConversationStep>? steps = null,
        IAuthMiddleware? auth = null,
        ILlmBridge? llm = null)
    {
        return new BotPipeline(
            store,
            new CommandRouter(handlers ?? [], NullLogger<CommandRouter>.Instance),
            new StepRunner(steps ?? [], NullLogger<StepRunner>.Instance),
            NullLogger<BotPipeline>.Instance,
            auth,
            llm);
    }

    private static BotContext CreateContext(string text, UserSession? session = null, SpyMessageSender? sender = null)
        => new()
        {
            ChatId = 123,
            Text = text,
            Session = session ?? CreateSession(),
            Sender = sender ?? new SpyMessageSender()
        };

    private static UserSession CreateSession(string? activeTopic = null, string? activeStepId = null)
        => new()
        {
            ChatId = 123,
            ActiveTopic = activeTopic,
            ActiveStepId = activeStepId,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

    private sealed class SpySessionStore : ISessionStore
    {
        public int SaveCallCount { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public List<UserSession> SavedSessions { get; } = [];

        public Task<UserSession?> GetAsync(long chatId, CancellationToken ct = default) => Task.FromResult<UserSession?>(null);

        public Task SaveAsync(UserSession session, CancellationToken ct = default)
        {
            SaveCallCount++;
            LastCancellationToken = ct;
            SavedSessions.Add(session);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(long chatId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class SpyMessageSender : IMessageSender
    {
        public List<(long ChatId, string Text)> Messages { get; } = [];

        public Task SendTextAsync(long chatId, string text, CancellationToken ct = default)
        {
            Messages.Add((chatId, text));
            return Task.CompletedTask;
        }

        public Task SendTextAsync(long chatId, string text, IReadOnlyList<string>? quickReplies, CancellationToken ct = default)
            => SendTextAsync(chatId, text, ct);
    }

    private sealed class StubAuth(bool allowed) : IAuthMiddleware
    {
        public int CallCount { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public Exception? ExceptionToThrow { get; init; }

        public Task<bool> IsAllowedAsync(BotContext context, CancellationToken ct = default)
        {
            CallCount++;
            LastCancellationToken = ct;
            if (ExceptionToThrow is not null) throw ExceptionToThrow;
            return Task.FromResult(allowed);
        }
    }

    private sealed class SpyLlmBridge(string reply) : ILlmBridge
    {
        public int CallCount { get; private set; }
        public string? LastMessage { get; private set; }
        public Exception? ExceptionToThrow { get; init; }

        public Task<string> ProcessAsync(string userMessage, BotContext context, CancellationToken ct = default)
        {
            CallCount++;
            LastMessage = userMessage;
            if (ExceptionToThrow is not null) throw ExceptionToThrow;
            return Task.FromResult(reply);
        }
    }

    private sealed class SpyTopicHandler(string command, FlowResult result) : ITopicHandler
    {
        public string Command { get; } = command;
        public int CallCount { get; private set; }
        public BotContext? LastContext { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public Exception? ExceptionToThrow { get; init; }

        public Task<FlowResult> HandleAsync(BotContext context, CancellationToken ct = default)
        {
            CallCount++;
            LastContext = context;
            LastCancellationToken = ct;
            if (ExceptionToThrow is not null) throw ExceptionToThrow;
            return Task.FromResult(result);
        }
    }

    private sealed class SpyConversationStep(string stepId) : IConversationStep
    {
        public string StepId { get; } = stepId;
        public bool ValidateResult { get; init; } = true;
        public StepResult ProcessResult { get; init; } = StepResult.Complete();
        public int ValidateCallCount { get; private set; }
        public int ProcessCallCount { get; private set; }
        public Exception? ExceptionToThrowOnValidate { get; init; }

        public Task AskAsync(BotContext context, CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> ValidateAsync(BotContext context, CancellationToken ct = default)
        {
            ValidateCallCount++;
            if (ExceptionToThrowOnValidate is not null) throw ExceptionToThrowOnValidate;
            return Task.FromResult(ValidateResult);
        }

        public Task<StepResult> ProcessAsync(BotContext context, CancellationToken ct = default)
        {
            ProcessCallCount++;
            return Task.FromResult(ProcessResult);
        }
    }
}
