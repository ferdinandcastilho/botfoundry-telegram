using BotFoundry.Telegram.Abstractions.Models;

namespace BotFoundry.Telegram.Abstractions.Ports
{
    public interface ISessionStore
    {
        Task<UserSession?> GetAsync(long chatId, CancellationToken ct = default);
        Task SaveAsync(UserSession session, CancellationToken ct = default);
        Task DeleteAsync(long chatId, CancellationToken ct = default);
    }
}
