using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BotForge.Telegram.Abstractions.Ports;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace BotForge.Telegram.Adapters
{
    internal sealed class TelegramMessageSender : IMessageSender
    {
        private readonly TelegramBotClient _client;
        public TelegramMessageSender(TelegramBotClient client) => _client = client;

        public Task SendTextAsync(long chatId, string text, CancellationToken ct = default)
            => _client.SendMessage(chatId, text, cancellationToken: ct);

        public Task SendTextAsync(long chatId, string text, IReadOnlyList<string>? quickReplies, CancellationToken ct = default)
        {
            if (quickReplies is null or { Count: 0 })
                return SendTextAsync(chatId, text, ct);

            var buttons = quickReplies.Select(r => new KeyboardButton(r)).ToArray();
            var markup = new ReplyKeyboardMarkup(new[] { buttons })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };

            return _client.SendMessage(chatId, text, replyMarkup: markup, cancellationToken: ct);
        }
    }
}
