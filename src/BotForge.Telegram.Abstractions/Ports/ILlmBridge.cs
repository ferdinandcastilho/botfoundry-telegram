using BotForge.Telegram.Abstractions.Models;

namespace BotForge.Telegram.Abstractions.Ports
{
    public interface ILlmBridge
    {
        Task<string> ProcessAsync(string userMessage, BotContext context, CancellationToken ct = default);
    }
}
