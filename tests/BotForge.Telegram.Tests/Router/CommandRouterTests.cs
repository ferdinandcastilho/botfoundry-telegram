using System;
using System.Threading;
using System.Threading.Tasks;
using BotForge.Telegram.Abstractions.Models;
using BotForge.Telegram.Abstractions.Ports;
using BotForge.Telegram.Core;
using BotForge.Telegram.Tests.Fakes;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BotForge.Telegram.Tests.Router
{
    public class CommandRouterTests
    {
        [Fact]
        public async Task RouteAsync_KnownCommand_InvokesHandler()
        {
            var handler = Substitute.For<ITopicHandler>();
            handler.Command.Returns("start");
            handler.HandleAsync(Arg.Any<BotContext>(), Arg.Any<CancellationToken>())
                .Returns(FlowResult.Complete());

            var router = new CommandRouter(new[] { handler }, new FakeLogger<CommandRouter>());
            var session = new UserSession { ChatId = 123 };
            var context = new BotContext
            {
                ChatId = 123,
                Text = "/start",
                Session = session,
                Sender = new FakeMessageSender()
            };

            await router.RouteAsync(context);

            await handler.Received(1).HandleAsync(context, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RouteAsync_UnknownCommand_DoesNotThrow()
        {
            var handler = Substitute.For<ITopicHandler>();
            handler.Command.Returns("start");

            var router = new CommandRouter(new[] { handler }, new FakeLogger<CommandRouter>());
            var session = new UserSession { ChatId = 123 };
            var context = new BotContext
            {
                ChatId = 123,
                Text = "/unknown",
                Session = session,
                Sender = new FakeMessageSender()
            };

            Func<Task> act = () => router.RouteAsync(context);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task RouteAsync_HandlerContinues_SetsActiveTopic()
        {
            var handler = Substitute.For<ITopicHandler>();
            handler.Command.Returns("start");
            handler.HandleAsync(Arg.Any<BotContext>(), Arg.Any<CancellationToken>())
                .Returns(FlowResult.Continue());

            var router = new CommandRouter(new[] { handler }, new FakeLogger<CommandRouter>());
            var session = new UserSession { ChatId = 123 };
            var context = new BotContext
            {
                ChatId = 123,
                Text = "/start",
                Session = session,
                Sender = new FakeMessageSender()
            };

            await router.RouteAsync(context);

            context.Session.ActiveTopic.Should().Be("start");
        }
    }
}
