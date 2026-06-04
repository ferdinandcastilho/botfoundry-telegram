using BotForge.Telegram.Abstractions.Ports;

namespace BotForge.Telegram.Tests.Fakes
{
    internal sealed class FakeMessageSender : IMessageSender
    {
        public List<(long ChatId, string Text)> SentMessages { get; } = [];

        public Task SendTextAsync(long chatId, string text, CancellationToken ct = default)
        {
            SentMessages.Add((chatId, text));
            return Task.CompletedTask;
        }

        public Task SendTextAsync(long chatId, string text, IReadOnlyList<string>? quickReplies, CancellationToken ct = default)
            => SendTextAsync(chatId, text, ct);
    }
}
