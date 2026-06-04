using BotForge.Telegram.Abstractions.Models;

namespace BotForge.Telegram.Abstractions.Ports
{
    public interface IAuthMiddleware
    {
        Task<bool> IsAllowedAsync(BotContext context, CancellationToken ct = default);
    }
}
