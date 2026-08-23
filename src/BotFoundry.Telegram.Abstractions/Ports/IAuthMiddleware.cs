using BotFoundry.Telegram.Abstractions.Models;

namespace BotFoundry.Telegram.Abstractions.Ports
{
    public interface IAuthMiddleware
    {
        Task<bool> IsAllowedAsync(BotContext context, CancellationToken ct = default);
    }
}
