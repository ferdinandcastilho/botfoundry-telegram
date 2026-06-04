using BotForge.Telegram.Abstractions.Models;
using BotForge.Telegram.Abstractions.Ports;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotForge.Telegram.Core.Tests;

public sealed class StepRunnerTests
{
    [Fact]
    public async Task ResumeAsync_ActiveStepIdNull_ClearsFlow()
    {
        var context = CreateContext(activeTopic: "flow", activeStepId: null);
        var runner = CreateRunner([]);

        await runner.ResumeAsync(context);

        context.Session.HasActiveFlow.Should().BeFalse();
        context.Session.ActiveStepId.Should().BeNull();
    }

    [Fact]
    public async Task ResumeAsync_UnknownStepId_ClearsFlow()
    {
        var context = CreateContext(activeTopic: "flow", activeStepId: "missing");
        var runner = CreateRunner([new SpyConversationStep("step1")]);

        await runner.ResumeAsync(context);

        context.Session.HasActiveFlow.Should().BeFalse();
        context.Session.ActiveStepId.Should().BeNull();
    }

    [Fact]
    public async Task ResumeAsync_InvalidValidation_ReasksCurrentStep()
    {
        var step = new SpyConversationStep("step1") { ValidateResult = false };
        var runner = CreateRunner([step]);

        await runner.ResumeAsync(CreateContext());

        step.AskCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ResumeAsync_InvalidValidation_DoesNotProcessStep()
    {
        var step = new SpyConversationStep("step1") { ValidateResult = false };
        var runner = CreateRunner([step]);

        await runner.ResumeAsync(CreateContext());

        step.ProcessCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ResumeAsync_InvalidValidation_KeepsFlowActive()
    {
        var step = new SpyConversationStep("step1") { ValidateResult = false };
        var context = CreateContext();
        var runner = CreateRunner([step]);

        await runner.ResumeAsync(context);

        context.Session.ActiveTopic.Should().Be("flow");
        context.Session.ActiveStepId.Should().Be("step1");
    }

    [Fact]
    public async Task ResumeAsync_StepResultComplete_ClearsFlow()
    {
        var step = new SpyConversationStep("step1") { ProcessResult = StepResult.Complete() };
        var context = CreateContext();
        context.Session.Data["key"] = "value";
        var runner = CreateRunner([step]);

        await runner.ResumeAsync(context);

        context.Session.ActiveTopic.Should().BeNull();
        context.Session.ActiveStepId.Should().BeNull();
        context.Session.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task ResumeAsync_StepResultContinue_KeepsFlowActive()
    {
        var step = new SpyConversationStep("step1") { ProcessResult = StepResult.Continue() };
        var context = CreateContext();
        var runner = CreateRunner([step]);

        await runner.ResumeAsync(context);

        context.Session.ActiveTopic.Should().Be("flow");
    }

    [Fact]
    public async Task ResumeAsync_StepResultJumpTo_UpdatesActiveStepId()
    {
        var step1 = new SpyConversationStep("step1") { ProcessResult = StepResult.JumpTo("step2") };
        var step2 = new SpyConversationStep("step2");
        var context = CreateContext();
        var runner = CreateRunner([step1, step2]);

        await runner.ResumeAsync(context);

        context.Session.ActiveStepId.Should().Be("step2");
    }

    [Fact]
    public async Task ResumeAsync_StepResultJumpTo_AsksTargetStep()
    {
        var step1 = new SpyConversationStep("step1") { ProcessResult = StepResult.JumpTo("step2") };
        var step2 = new SpyConversationStep("step2");
        var runner = CreateRunner([step1, step2]);

        await runner.ResumeAsync(CreateContext());

        step2.AskCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ResumeAsync_JumpToMissingStep_DoesNotThrow()
    {
        var step = new SpyConversationStep("step1") { ProcessResult = StepResult.JumpTo("missing") };
        var runner = CreateRunner([step]);
        var act = () => runner.ResumeAsync(CreateContext());

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("STEP1")]
    [InlineData("Step1")]
    [InlineData("step1")]
    public async Task ResumeAsync_StepIdMatchingIsCaseInsensitive(string activeStepId)
    {
        var step = new SpyConversationStep("step1") { ProcessResult = StepResult.Continue() };
        var runner = CreateRunner([step]);

        await runner.ResumeAsync(CreateContext(activeStepId: activeStepId));

        step.ValidateCallCount.Should().Be(1);
        step.ProcessCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ResumeAsync_NullSteps_ClearsFlowWithoutThrowing()
    {
        var context = CreateContext();
        var runner = CreateRunner(null);

        await runner.ResumeAsync(context);

        context.Session.HasActiveFlow.Should().BeFalse();
    }

    [Fact]
    public async Task ResumeAsync_Continue_PreservesActiveStepIdWhenStepDoesNotChangeIt()
    {
        var step = new SpyConversationStep("step1") { ProcessResult = StepResult.Continue() };
        var context = CreateContext();
        var runner = CreateRunner([step]);

        await runner.ResumeAsync(context);

        context.Session.ActiveStepId.Should().Be("step1");
    }

    [Fact]
    public async Task ResumeAsync_Continue_AllowsStepToAdvanceActiveStepId()
    {
        var step = new SpyConversationStep("step1")
        {
            ProcessResult = StepResult.Continue(),
            OnProcess = ctx => ctx.Session.ActiveStepId = "step2"
        };
        var context = CreateContext();
        var runner = CreateRunner([step, new SpyConversationStep("step2")]);

        await runner.ResumeAsync(context);

        context.Session.ActiveStepId.Should().Be("step2");
    }

    [Fact]
    public async Task ResumeAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var step = new SpyConversationStep("step1") { ProcessResult = StepResult.JumpTo("step2") };
        var step2 = new SpyConversationStep("step2");
        var runner = CreateRunner([step, step2]);

        await runner.ResumeAsync(CreateContext(), cts.Token);

        step.ValidateCancellationToken.Should().Be(cts.Token);
        step.ProcessCancellationToken.Should().Be(cts.Token);
        step2.AskCancellationToken.Should().Be(cts.Token);
    }

    private static StepRunner CreateRunner(IEnumerable<IConversationStep>? steps)
        => new(steps!, NullLogger<StepRunner>.Instance);

    private static BotContext CreateContext(string? activeTopic = "flow", string? activeStepId = "step1")
        => new()
        {
            ChatId = 123,
            Text = "answer",
            Session = new UserSession
            {
                ChatId = 123,
                ActiveTopic = activeTopic,
                ActiveStepId = activeStepId,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
            },
            Sender = new StubMessageSender()
        };

    private sealed class SpyConversationStep(string stepId) : IConversationStep
    {
        public string StepId { get; } = stepId;
        public bool ValidateResult { get; init; } = true;
        public StepResult ProcessResult { get; init; } = StepResult.Complete();
        public Action<BotContext>? OnProcess { get; init; }
        public int AskCallCount { get; private set; }
        public int ValidateCallCount { get; private set; }
        public int ProcessCallCount { get; private set; }
        public CancellationToken AskCancellationToken { get; private set; }
        public CancellationToken ValidateCancellationToken { get; private set; }
        public CancellationToken ProcessCancellationToken { get; private set; }

        public Task AskAsync(BotContext context, CancellationToken ct = default)
        {
            AskCallCount++;
            AskCancellationToken = ct;
            return Task.CompletedTask;
        }

        public Task<bool> ValidateAsync(BotContext context, CancellationToken ct = default)
        {
            ValidateCallCount++;
            ValidateCancellationToken = ct;
            return Task.FromResult(ValidateResult);
        }

        public Task<StepResult> ProcessAsync(BotContext context, CancellationToken ct = default)
        {
            ProcessCallCount++;
            ProcessCancellationToken = ct;
            OnProcess?.Invoke(context);
            return Task.FromResult(ProcessResult);
        }
    }

    private sealed class StubMessageSender : IMessageSender
    {
        public Task SendTextAsync(long chatId, string text, CancellationToken ct = default) => Task.CompletedTask;

        public Task SendTextAsync(long chatId, string text, IReadOnlyList<string>? quickReplies, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
