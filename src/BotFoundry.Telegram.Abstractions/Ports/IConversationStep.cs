using BotFoundry.Telegram.Abstractions.Models;

namespace BotFoundry.Telegram.Abstractions.Ports
{
    public interface IConversationStep
    {
        string StepId { get; }
        Task AskAsync(BotContext context, CancellationToken ct = default);
        Task<bool> ValidateAsync(BotContext context, CancellationToken ct = default);
        Task<StepResult> ProcessAsync(BotContext context, CancellationToken ct = default);
    }
}
