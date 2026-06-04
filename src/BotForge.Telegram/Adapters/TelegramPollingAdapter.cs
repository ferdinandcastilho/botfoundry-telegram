using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BotForge.Telegram.Abstractions.Ports;
using BotForge.Telegram.Abstractions.Models;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BotForge.Telegram.Adapters
{
    internal sealed class TelegramPollingAdapter(
        ITelegramBotClient client,
        IBotPipeline pipeline,
        IMessageSender sender,
        ISessionStore sessionStore,
        ILogger<TelegramPollingAdapter> logger) : IHostedService
    {
        public Task StartAsync(CancellationToken ct)
        {
            var options = new ReceiverOptions { AllowedUpdates = [UpdateType.Message] };
            client.StartReceiving(HandleUpdateAsync, HandleErrorAsync, options, ct);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

        private async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken ct)
        {
            if (update.Message?.Text is not { } text) return;

            var chatId = update.Message.Chat.Id;
            var session = await sessionStore.GetAsync(chatId, ct)
                          ?? new UserSession { ChatId = chatId, ExpiresAt = DateTimeOffset.UtcNow.AddHours(24) };

            var context = new BotContext { ChatId = chatId, Text = text, Session = session, Sender = sender };

            await pipeline.ProcessAsync(context, ct);

            if (session.IsExpired)
                await sessionStore.DeleteAsync(chatId, ct);
            else
                await sessionStore.SaveAsync(session, ct);
        }

        private Task HandleErrorAsync(ITelegramBotClient _, Exception ex, CancellationToken ct)
        {
            logger.LogError(ex, "Polling error");
            return Task.CompletedTask;
        }
    }
}
