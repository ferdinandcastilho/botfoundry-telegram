using BotForge.Telegram.Abstractions.Models;
using BotForge.Telegram.Abstractions.Ports;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotForge.Telegram.Core.Tests;

public sealed class CommandRouterTests
{
    [Fact]
    public async Task RouteAsync_RegisteredCommand_InvokesCorrectHandler()
    {
        var handler = new SpyTopicHandler("start", FlowResult.Complete());
        var router = CreateRouter([handler]);
        var context = CreateContext("/start");

        await router.RouteAsync(context);

        handler.CallCount.Should().Be(1);
        handler.LastContext.Should().BeSameAs(context);
    }

    [Fact]
    public async Task RouteAsync_UnknownCommand_DoesNotThrow()
    {
        var router = CreateRouter([new SpyTopicHandler("start", FlowResult.Complete())]);
        var act = () => router.RouteAsync(CreateContext("/unknown"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RouteAsync_UnknownCommand_DoesNotInvokeAnyHandler()
    {
        var handler = new SpyTopicHandler("start", FlowResult.Complete());
        var router = CreateRouter([handler]);

        await router.RouteAsync(CreateContext("/unknown"));

        handler.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("/Start")]
    [InlineData("/START")]
    [InlineData("/sTaRt")]
    public async Task RouteAsync_CommandMatchingIsCaseInsensitive(string text)
    {
        var handler = new SpyTopicHandler("start", FlowResult.Complete());
        var router = CreateRouter([handler]);

        await router.RouteAsync(CreateContext(text));

        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RouteAsync_CommandWithArguments_InvokesMatchingHandler()
    {
        var handler = new SpyTopicHandler("start", FlowResult.Complete());
        var router = CreateRouter([handler]);

        await router.RouteAsync(CreateContext("/start arg1 arg2"));

        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RouteAsync_FlowResultContinue_SetsActiveTopic()
    {
        var handler = new SpyTopicHandler("start", FlowResult.Continue());
        var context = CreateContext("/start");
        var router = CreateRouter([handler]);

        await router.RouteAsync(context);

        context.Session.ActiveTopic.Should().Be("start");
    }

    [Fact]
    public async Task RouteAsync_FlowResultComplete_KeepsActiveTopicNull()
    {
        var handler = new SpyTopicHandler("start", FlowResult.Complete());
        var context = CreateContext("/start");
        var router = CreateRouter([handler]);

        await router.RouteAsync(context);

        context.Session.ActiveTopic.Should().BeNull();
    }

    [Fact]
    public async Task RouteAsync_MultipleHandlers_InvokesOnlyMatchingHandler()
    {
        var start = new SpyTopicHandler("start", FlowResult.Complete());
        var help = new SpyTopicHandler("help", FlowResult.Complete());
        var router = CreateRouter([start, help]);

        await router.RouteAsync(CreateContext("/help"));

        start.CallCount.Should().Be(0);
        help.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RouteAsync_SlashOnly_DoesNotInvokeAnyHandler()
    {
        var handler = new SpyTopicHandler("start", FlowResult.Complete());
        var router = CreateRouter([handler]);

        await router.RouteAsync(CreateContext("/"));

        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task RouteAsync_HandlerCommandMatchingIsCaseInsensitive()
    {
        var handler = new SpyTopicHandler("Start", FlowResult.Complete());
        var router = CreateRouter([handler]);

        await router.RouteAsync(CreateContext("/start"));

        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RouteAsync_CommandWithExtraSpaces_InvokesMatchingHandler()
    {
        var handler = new SpyTopicHandler("start", FlowResult.Complete());
        var router = CreateRouter([handler]);

        await router.RouteAsync(CreateContext("/start   arg1"));

        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RouteAsync_NullHandlers_DoesNotThrow()
    {
        var router = CreateRouter(null);
        var act = () => router.RouteAsync(CreateContext("/start"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RouteAsync_PropagatesCancellationTokenToHandler()
    {
        using var cts = new CancellationTokenSource();
        var handler = new SpyTopicHandler("start", FlowResult.Complete());
        var router = CreateRouter([handler]);

        await router.RouteAsync(CreateContext("/start"), cts.Token);

        handler.LastCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task RouteAsync_Continue_NormalizesActiveTopicToLowerInvariant()
    {
        var handler = new SpyTopicHandler("Start", FlowResult.Continue());
        var context = CreateContext("/START");
        var router = CreateRouter([handler]);

        await router.RouteAsync(context);

        context.Session.ActiveTopic.Should().Be("start");
    }

    private static CommandRouter CreateRouter(IEnumerable<ITopicHandler>? handlers)
        => new(handlers!, NullLogger<CommandRouter>.Instance);

    private static BotContext CreateContext(string text)
        => new()
        {
            ChatId = 123,
            Text = text,
            Session = new UserSession { ChatId = 123, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) },
            Sender = new StubMessageSender()
        };

    private sealed class SpyTopicHandler(string command, FlowResult result) : ITopicHandler
    {
        public string Command { get; } = command;
        public int CallCount { get; private set; }
        public BotContext? LastContext { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public Task<FlowResult> HandleAsync(BotContext context, CancellationToken ct = default)
        {
            CallCount++;
            LastContext = context;
            LastCancellationToken = ct;
            return Task.FromResult(result);
        }
    }

    private sealed class StubMessageSender : IMessageSender
    {
        public Task SendTextAsync(long chatId, string text, CancellationToken ct = default) => Task.CompletedTask;

        public Task SendTextAsync(long chatId, string text, IReadOnlyList<string>? quickReplies, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}