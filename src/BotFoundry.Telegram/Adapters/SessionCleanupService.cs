using BotFoundry.Telegram.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BotFoundry.Telegram.Adapters
{
    internal sealed class SessionCleanupService(InMemorySessionStore store, TimeSpan interval) : BackgroundService
    {
        public SessionCleanupService(InMemorySessionStore store, BotFoundryOptions options)
            : this(store, TimeSpan.FromSeconds(options.SessionCleanupIntervalSeconds))
        {
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(interval);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                    store.RemoveExpired();
            }
            catch (OperationCanceledException)
            {
                // Host is stopping.
            }
        }
    }
}
