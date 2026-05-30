using System.Threading;
using System.Threading.Tasks;
using BotForge.Telegram.Abstractions.Models;

namespace BotForge.Telegram.Abstractions.Ports
{
    public interface IBotPipeline
    {
        Task ProcessAsync(BotContext context, CancellationToken ct = default);
    }
}
