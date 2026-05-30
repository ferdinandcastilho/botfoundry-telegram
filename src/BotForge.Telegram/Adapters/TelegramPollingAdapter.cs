using System;
using System.Threading;
using System.Threading.Tasks;
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
    internal sealed class TelegramPollingAdapter : IHostedService
    {
        private readonly ITelegramBotClient _client;
        private readonly IBotPipeline _pipeline;
        private readonly IMessageSender _sender;
        private readonly ISessionStore _sessionStore;
        private readonly ILogger<TelegramPollingAdapter> _logger;

        public TelegramPollingAdapter(
            ITelegramBotClient client,
            IBotPipeline pipeline,
            IMessageSender sender,
            ISessionStore sessionStore,
            ILogger<TelegramPollingAdapter> logger)
        {
            _client = client;
            _pipeline = pipeline;
            _sender = sender;
            _sessionStore = sessionStore;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken ct)
        {
            var options = new ReceiverOptions { AllowedUpdates = new[] { UpdateType.Message } };
            _client.StartReceiving(HandleUpdateAsync, HandleErrorAsync, options, ct);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

        private async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken ct)
        {
            if (update.Message?.Text is not { } text) return;

            var chatId = update.Message.Chat.Id;
            var session = await _sessionStore.GetAsync(chatId, ct)
                          ?? new UserSession { ChatId = chatId, ExpiresAt = DateTimeOffset.UtcNow.AddHours(24) };

            var context = new BotContext { ChatId = chatId, Text = text, Session = session, Sender = _sender };

            await _pipeline.ProcessAsync(context, ct);

            if (session.IsExpired)
                await _sessionStore.DeleteAsync(chatId, ct);
            else
                await _sessionStore.SaveAsync(session, ct);
        }

        private Task HandleErrorAsync(ITelegramBotClient _, Exception ex, CancellationToken ct)
        {
            _logger.LogError(ex, "Polling error");
            return Task.CompletedTask;
        }
    }
}
