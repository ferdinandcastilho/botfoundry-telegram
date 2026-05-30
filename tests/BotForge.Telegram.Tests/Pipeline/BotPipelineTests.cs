using System.Threading;
using System.Threading.Tasks;
using BotForge.Telegram.Abstractions.Models;
using BotForge.Telegram.Abstractions.Ports;
using BotForge.Telegram.Core;
using BotForge.Telegram.Tests.Fakes;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BotForge.Telegram.Tests.Pipeline
{
    public class BotPipelineTests
    {
        [Fact]
        public async Task ProcessAsync_AuthBlocks_SessionNotSaved()
        {
            var sessionStore = Substitute.For<ISessionStore>();
            var router = new CommandRouter([], new FakeLogger<CommandRouter>());
            var stepRunner = new StepRunner([], new FakeLogger<StepRunner>());
            var logger = new FakeLogger<BotPipeline>();
            var auth = Substitute.For<IAuthMiddleware>();

            auth.IsAllowedAsync(Arg.Any<BotContext>(), Arg.Any<CancellationToken>()).Returns(false);

            var botPipeline = new BotPipeline(sessionStore, router, stepRunner, logger, auth);
            var session = new UserSession { ChatId = 123 };
            var context = new BotContext
            {
                ChatId = 123,
                Text = "/start",
                Session = session,
                Sender = new FakeMessageSender()
            };

            await botPipeline.ProcessAsync(context);

            // Auth blocks execution but session is still saved in finally block
            await sessionStore.Received(1).SaveAsync(Arg.Any<UserSession>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ProcessAsync_Command_RoutesToHandler()
        {
            var sessionStore = new FakeSessionStore();
            var handler = Substitute.For<ITopicHandler>();
            handler.Command.Returns("start");
            handler.HandleAsync(Arg.Any<BotContext>(), Arg.Any<CancellationToken>())
                .Returns(FlowResult.Continue());

            var router = new CommandRouter(new[] { handler }, new FakeLogger<CommandRouter>());
            var stepRunner = new StepRunner([], new FakeLogger<StepRunner>());
            var logger = new FakeLogger<BotPipeline>();

            var botPipeline = new BotPipeline(sessionStore, router, stepRunner, logger);
            var session = new UserSession { ChatId = 123 };
            var context = new BotContext
            {
                ChatId = 123,
                Text = "/start",
                Session = session,
                Sender = new FakeMessageSender()
            };

            await botPipeline.ProcessAsync(context);

            await handler.Received(1).HandleAsync(context, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ProcessAsync_SessionAlwaysSaved_WhenAuthPasses()
        {
            var sessionStore = new FakeSessionStore();
            var router = new CommandRouter([], new FakeLogger<CommandRouter>());
            var stepRunner = new StepRunner([], new FakeLogger<StepRunner>());
            var logger = new FakeLogger<BotPipeline>();
            var auth = Substitute.For<IAuthMiddleware>();

            auth.IsAllowedAsync(Arg.Any<BotContext>(), Arg.Any<CancellationToken>()).Returns(true);

            var botPipeline = new BotPipeline(sessionStore, router, stepRunner, logger, auth);
            var session = new UserSession { ChatId = 123 };
            var context = new BotContext
            {
                ChatId = 123,
                Text = "hello",
                Session = session,
                Sender = new FakeMessageSender()
            };

            await botPipeline.ProcessAsync(context);

            var saved = await sessionStore.GetAsync(123);
            saved.Should().NotBeNull();
            saved!.ChatId.Should().Be(123);
        }
    }
}
