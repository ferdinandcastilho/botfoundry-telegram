using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BotForge.Telegram.Abstractions.Models;
using BotForge.Telegram.Abstractions.Ports;
using Microsoft.Extensions.Logging;

namespace BotForge.Telegram.Core
{
    internal sealed class StepRunner
    {
        private readonly IDictionary<string, IConversationStep> _steps;
        private readonly ILogger<StepRunner> _logger;

        public StepRunner(IEnumerable<IConversationStep> steps, ILogger<StepRunner> logger)
        {
            _logger = logger;
            _steps = steps?.ToDictionary(s => s.StepId, StringComparer.OrdinalIgnoreCase)
                      ?? new Dictionary<string, IConversationStep>(StringComparer.OrdinalIgnoreCase);
        }

        public async Task ResumeAsync(BotContext context, CancellationToken ct = default)
        {
            var stepId = context.Session.ActiveStepId;

            if (stepId is null || !_steps.TryGetValue(stepId, out var step))
            {
                _logger.LogWarning("Step não encontrado: {StepId}. Limpando fluxo.", stepId);
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
                    // ActiveStepId deve ser atualizado pelo step em ProcessAsync
                    break;
            }
        }
    }
}
