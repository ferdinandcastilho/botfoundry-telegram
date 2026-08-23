using BotFoundry.Telegram.Abstractions.Models;

namespace BotFoundry.Telegram.Abstractions.Ports
{
    public interface ITopicHandler
    {
        string Command { get; }
        Task<FlowResult> HandleAsync(BotContext context, CancellationToken ct = default);
    }
}
