using System.Threading;
using System.Threading.Tasks;
using BotForge.Telegram.Abstractions.Models;
using BotForge.Telegram.Abstractions.Ports;
using BotForge.Telegram.Core;
using BotForge.Telegram.Tests.Fakes;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BotForge.Telegram.Tests.StepRunnerTests
{
    public class StepRunnerTests
    {
        [Fact]
        public async Task ResumeAsync_StepComplete_ClearsFlow()
        {
            var step = Substitute.For<IConversationStep>();
            step.StepId.Returns("step1");
            step.ValidateAsync(Arg.Any<BotContext>(), Arg.Any<CancellationToken>()).Returns(true);
            step.ProcessAsync(Arg.Any<BotContext>(), Arg.Any<CancellationToken>())
                .Returns(StepResult.Complete());

            var stepRunner = new StepRunner(new[] { step }, new FakeLogger<StepRunner>());
            var session = new UserSession { ChatId = 123 };
            session.ActiveTopic = "test_flow";
            session.ActiveStepId = "step1";

            var context = new BotContext
            {
                ChatId = 123,
                Text = "input",
                Session = session,
                Sender = new FakeMessageSender()
            };

            await stepRunner.ResumeAsync(context);

            context.Session.HasActiveFlow.Should().BeFalse();
        }

        [Fact]
        public async Task ResumeAsync_InvalidInput_RepeatsQuestion()
        {
            var step = Substitute.For<IConversationStep>();
            step.StepId.Returns("step1");
            step.ValidateAsync(Arg.Any<BotContext>(), Arg.Any<CancellationToken>()).Returns(false);

            var stepRunner = new StepRunner(new[] { step }, new FakeLogger<StepRunner>());
            var session = new UserSession { ChatId = 123 };
            session.ActiveTopic = "test_flow";
            session.ActiveStepId = "step1";

            var context = new BotContext
            {
                ChatId = 123,
                Text = "invalid",
                Session = session,
                Sender = new FakeMessageSender()
            };

            await stepRunner.ResumeAsync(context);

            await step.Received(1).AskAsync(context, Arg.Any<CancellationToken>());
            await step.DidNotReceive().ProcessAsync(Arg.Any<BotContext>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ResumeAsync_JumpTo_AsksNextStep()
        {
            var step1 = Substitute.For<IConversationStep>();
            step1.StepId.Returns("step1");
            step1.ValidateAsync(Arg.Any<BotContext>(), Arg.Any<CancellationToken>()).Returns(true);
            step1.ProcessAsync(Arg.Any<BotContext>(), Arg.Any<CancellationToken>())
                .Returns(StepResult.JumpTo("step2"));

            var step2 = Substitute.For<IConversationStep>();
            step2.StepId.Returns("step2");

            var stepRunner = new StepRunner(new[] { step1, step2 }, new FakeLogger<StepRunner>());
            var session = new UserSession { ChatId = 123 };
            session.ActiveTopic = "test_flow";
            session.ActiveStepId = "step1";

            var context = new BotContext
            {
                ChatId = 123,
                Text = "input",
                Session = session,
                Sender = new FakeMessageSender()
            };

            await stepRunner.ResumeAsync(context);

            context.Session.ActiveStepId.Should().Be("step2");
            await step2.Received(1).AskAsync(context, Arg.Any<CancellationToken>());
        }
    }
}
