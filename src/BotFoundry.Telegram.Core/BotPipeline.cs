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
            var persistSession = true;

            try
            {
                // 1. Auth
                if (auth is not null)
                {
                    var allowed = await auth.IsAllowedAsync(context, ct);
                    if (!allowed)
                    {
                        // Denied chats never get a stored session.
                        persistSession = false;
                        return;
                    }
                }

                // 2. Active step
                if (context.Session.HasActiveFlow)
                {
                    await stepRunner.ResumeAsync(context, ct);
                    return;
                }

                // 3. Command
                if (!string.IsNullOrEmpty(context.Text) && context.Text.StartsWith('/'))
                {
                    await router.RouteAsync(context, ct);
                    return;
                }

                // 4. Free text → LLM
                if (llm is not null)
                {
                    var reply = await llm.ProcessAsync(context.Text, context, ct);
                    await context.SendAsync(reply, ct);
                }
            }
            finally
            {
                // 5. Persist the session unless access was denied
                if (persistSession)
                    await sessionStore.SaveAsync(context.Session, ct);
            }
        }
    }
}
