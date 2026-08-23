using BotFoundry.Telegram.Abstractions.Models;
using BotFoundry.Telegram.Abstractions.Ports;
using Microsoft.Extensions.Logging;

namespace BotFoundry.Telegram.Core
{
    internal sealed class CommandRouter(IEnumerable<ITopicHandler> handlers, ILogger<CommandRouter> logger)
    {
        private readonly IDictionary<string, ITopicHandler> _handlers = handlers?.ToDictionary(h => h.Command.ToLowerInvariant(), StringComparer.OrdinalIgnoreCase)
                        ?? new Dictionary<string, ITopicHandler>(StringComparer.OrdinalIgnoreCase);

        public async Task RouteAsync(BotContext context, CancellationToken ct = default)
        {
            var command = context.Text.Length > 1 ? context.Text[1..].Split(' ')[0].ToLowerInvariant() : string.Empty;

            if (string.IsNullOrEmpty(command) || !_handlers.TryGetValue(command, out var handler))
            {
                logger.LogWarning("Comando não registrado: {Command}", command);
                return;
            }

            var result = await handler.HandleAsync(context, ct);

            if (!result.IsComplete)
                context.Session.ActiveTopic = command;
        }
    }
}
