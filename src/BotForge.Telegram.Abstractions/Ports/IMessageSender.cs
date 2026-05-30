using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BotForge.Telegram.Abstractions.Ports
{
    public interface IMessageSender
    {
        Task SendTextAsync(long chatId, string text, CancellationToken ct = default);
        Task SendTextAsync(long chatId, string text, IReadOnlyList<string>? quickReplies, CancellationToken ct = default);
    }
}
