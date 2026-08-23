using BotFoundry.Telegram.Abstractions.Models;

namespace BotFoundry.Telegram.Abstractions.Ports
{
    public interface ILlmBridge
    {
        Task<string> ProcessAsync(string userMessage, BotContext context, CancellationToken ct = default);
    }
}
