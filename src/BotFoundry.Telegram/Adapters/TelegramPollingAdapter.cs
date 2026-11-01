using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BotFoundry.Telegram.Abstractions.Ports;
using BotFoundry.Telegram.Abstractions.Models;
using BotFoundry.Telegram.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BotFoundry.Telegram.Adapters
{
    internal sealed class TelegramPollingAdapter(
        ITelegramBotClient client,
        IBotPipeline pipeline,
        IMessageSender sender,
        ISessionStore sessionStore,
        BotFoundryOptions options,
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
            var session = await sessionStore.GetAsync(chatId, ct);

            // An expired session is discarded before the context is built, so it can never resume an old flow.
            if (session is not null && session.IsExpired)
            {
                await sessionStore.DeleteAsync(chatId, ct);
                session = null;
            }

            session ??= new UserSession { ChatId = chatId };
            session.ExpiresAt = DateTimeOffset.UtcNow.Add(options.SessionExpiry);

            var context = new BotContext { ChatId = chatId, Text = text, Session = session, Sender = sender };

            // The pipeline persists the session (or skips it when access is denied).
            await pipeline.ProcessAsync(context, ct);
        }

        private Task HandleErrorAsync(ITelegramBotClient _, Exception ex, CancellationToken ct)
        {
            logger.LogError(ex, "Polling error");
            return Task.CompletedTask;
        }
    }
}
