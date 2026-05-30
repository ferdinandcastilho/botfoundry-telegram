using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BotForge.Telegram.Abstractions.Models;
using BotForge.Telegram.Abstractions.Ports;
using Microsoft.Extensions.Logging;

namespace BotForge.Telegram.Core
{
    internal sealed class CommandRouter
    {
        private readonly IDictionary<string, ITopicHandler> _handlers;
        private readonly ILogger<CommandRouter> _logger;

        public CommandRouter(IEnumerable<ITopicHandler> handlers, ILogger<CommandRouter> logger)
        {
            _logger = logger;
            _handlers = handlers?.ToDictionary(h => h.Command.ToLowerInvariant(), StringComparer.OrdinalIgnoreCase)
                        ?? new Dictionary<string, ITopicHandler>(StringComparer.OrdinalIgnoreCase);
        }

        public async Task RouteAsync(BotContext context, CancellationToken ct = default)
        {
            var command = context.Text.Length > 1 ? context.Text[1..].Split(' ')[0].ToLowerInvariant() : string.Empty;

            if (string.IsNullOrEmpty(command) || !_handlers.TryGetValue(command, out var handler))
            {
                _logger.LogWarning("Comando não registrado: {Command}", command);
                return;
            }

            var result = await handler.HandleAsync(context, ct);

            if (!result.IsComplete)
                context.Session.ActiveTopic = command;
        }
    }
}
