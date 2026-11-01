using BotFoundry.Telegram.Abstractions.Models;
using BotFoundry.Telegram.Abstractions.Ports;
using Microsoft.Extensions.Logging;

namespace BotFoundry.Telegram.Core
{
    internal sealed class StepRunner(IEnumerable<IConversationStep> steps, ILogger<StepRunner> logger)
    {
        private readonly IDictionary<string, IConversationStep> _steps = steps?.ToDictionary(s => s.StepId, StringComparer.OrdinalIgnoreCase)
                      ?? new Dictionary<string, IConversationStep>(StringComparer.OrdinalIgnoreCase);

        public async Task ResumeAsync(BotContext context, CancellationToken ct = default)
        {
            var stepId = context.Session.ActiveStepId;

            if (stepId is null || !_steps.TryGetValue(stepId, out var step))
            {
                logger.LogWarning("Step not found: {StepId}. Clearing conversation flow.", stepId);
                context.Session.ClearFlow();
                return;
            }

            var valid = await step.ValidateAsync(context, ct);
            if (!valid)
            {
                await step.AskAsync(context, ct);
                return;
            }

            var result = await step.ProcessAsync(context, ct);

            switch (result.Kind)
            {
                case StepResultKind.Complete:
                    context.Session.ClearFlow();
                    break;
                case StepResultKind.JumpTo:
                    context.Session.ActiveStepId = result.TargetId;
                    if (result.TargetId is not null && _steps.TryGetValue(result.TargetId, out var next))
                        await next.AskAsync(context, ct);
                    break;
                case StepResultKind.Continue:
                default:
                    // The step must update ActiveStepId in ProcessAsync
                    break;
            }
        }
    }
}
