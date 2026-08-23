using BotFoundry.Telegram.Abstractions.Ports;

namespace BotFoundry.Telegram.Abstractions.Models
{
    public sealed class BotContext
    {
        public required long ChatId { get; init; }
        public required string Text { get; init; }
        public required UserSession Session { get; init; }
        public required IMessageSender Sender { get; init; }

        public Task SendAsync(string text, CancellationToken ct = default)
            => Sender.SendTextAsync(ChatId, text, ct);

        public Task SendAsync(string text, IReadOnlyList<string> quickReplies, CancellationToken ct = default)
            => Sender.SendTextAsync(ChatId, text, quickReplies, ct);
    }
}
