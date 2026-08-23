using BotFoundry.Telegram.Abstractions.Models;
using BotFoundry.Telegram.Abstractions.Ports;
using Microsoft.Extensions.Logging;

namespace BotFoundry.Telegram.Core
{
    internal sealed class BotPipeline(ISessionStore sessionStore,
        CommandRouter router,
        StepRunner stepRunner,
        ILogger<BotPipeline> logger,
        IAuthMiddleware? auth = null,
        ILlmBridge? llm = null) : IBotPipeline
    {
        public async Task ProcessAsync(BotContext context, CancellationToken ct = default)
        {
            try
            {
                // 1. Auth
                if (auth is not null)
                {
                    var allowed = await auth.IsAllowedAsync(context, ct);
                    if (!allowed) return;
                }

                // 2. Step ativo
                if (context.Session.HasActiveFlow)
                {
                    await stepRunner.ResumeAsync(context, ct);
                    return;
                }

                // 3. Comando
                if (!string.IsNullOrEmpty(context.Text) && context.Text.StartsWith('/'))
                {
                    await router.RouteAsync(context, ct);
                    return;
                }

                // 4. Texto livre → LLM
                if (llm is not null)
                {
                    var reply = await llm.ProcessAsync(context.Text, context, ct);
                    await context.SendAsync(reply, ct);
                }
            }
            finally
            {
                // 5. Persiste sessão sempre
                await sessionStore.SaveAsync(context.Session, ct);
            }
        }
    }
}
