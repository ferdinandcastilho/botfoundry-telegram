using BotForge.Telegram.Abstractions.Models;

namespace BotForge.Telegram.Abstractions.Ports
{
    public interface ITopicHandler
    {
        string Command { get; }
        Task<FlowResult> HandleAsync(BotContext context, CancellationToken ct = default);
    }
}
