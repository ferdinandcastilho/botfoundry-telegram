using BotFoundry.Telegram.Abstractions.Models;

namespace BotFoundry.Telegram.Abstractions.Ports
{
    public interface IBotPipeline
    {
        Task ProcessAsync(BotContext context, CancellationToken ct = default);
    }
}
