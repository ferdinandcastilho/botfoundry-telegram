using System.Threading;
using System.Threading.Tasks;
using BotForge.Telegram.Abstractions.Models;
using BotForge.Telegram.Abstractions.Ports;
using Microsoft.Extensions.Logging;

namespace BotForge.Telegram.Core
{
    internal sealed class BotPipeline : IBotPipeline
    {
        private readonly ISessionStore _sessionStore;
        private readonly CommandRouter _router;
        private readonly StepRunner _stepRunner;
        private readonly ILogger<BotPipeline> _logger;
        private readonly IAuthMiddleware? _auth;
        private readonly ILlmBridge? _llm;

        public BotPipeline(ISessionStore sessionStore,
            CommandRouter router,
            StepRunner stepRunner,
            ILogger<BotPipeline> logger,
            IAuthMiddleware? auth = null,
            ILlmBridge? llm = null)
        {
            _sessionStore = sessionStore;
            _router = router;
            _stepRunner = stepRunner;
            _logger = logger;
            _auth = auth;
            _llm = llm;
        }

        public async Task ProcessAsync(BotContext context, CancellationToken ct = default)
        {
            try
            {
                // 1. Auth
                if (_auth is not null)
                {
                    var allowed = await _auth.IsAllowedAsync(context, ct);
                    if (!allowed) return;
                }

                // 2. Step ativo
                if (context.Session.HasActiveFlow)
                {
                    await _stepRunner.ResumeAsync(context, ct);
                    return;
                }

                // 3. Comando
                if (!string.IsNullOrEmpty(context.Text) && context.Text.StartsWith('/'))
                {
                    await _router.RouteAsync(context, ct);
                    return;
                }

                // 4. Texto livre → LLM
                if (_llm is not null)
                {
                    var reply = await _llm.ProcessAsync(context.Text, context, ct);
                    await context.SendAsync(reply, ct);
                }
            }
            finally
            {
                // 5. Persiste sessão sempre
                await _sessionStore.SaveAsync(context.Session, ct);
            }
        }
    }
}
