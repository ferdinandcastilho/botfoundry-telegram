using System.Threading;
using System.Threading.Tasks;
using BotForge.Telegram.Abstractions.Models;

namespace BotForge.Telegram.Abstractions.Ports
{
    public interface ISessionStore
    {
        Task<UserSession?> GetAsync(long chatId, CancellationToken ct = default);
        Task SaveAsync(UserSession session, CancellationToken ct = default);
        Task DeleteAsync(long chatId, CancellationToken ct = default);
    }
}
