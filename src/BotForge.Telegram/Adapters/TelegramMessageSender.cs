using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BotForge.Telegram.Abstractions.Ports;

namespace BotForge.Telegram.Adapters
{
    internal sealed class TelegramMessageSender : IMessageSender
    {
        public TelegramMessageSender() { }

        public Task SendTextAsync(long chatId, string text, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SendTextAsync(long chatId, string text, IReadOnlyList<string>? quickReplies, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
