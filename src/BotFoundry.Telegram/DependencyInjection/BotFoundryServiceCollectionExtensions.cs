using Microsoft.Extensions.DependencyInjection;
using BotFoundry.Telegram.Abstractions.Ports;
using BotFoundry.Telegram.Adapters;

namespace BotFoundry.Telegram.DependencyInjection
{
    public static class BotFoundryServiceCollectionExtensions
    {
        public static IServiceCollection AddBotFoundry(
            this IServiceCollection services,
            string token,
            Action<BotFoundryOptions>? configure = null)
        {
            var options = new BotFoundryOptions();
            configure?.Invoke(options);

            if (options.SessionCleanupIntervalSeconds <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(configure),
                    options.SessionCleanupIntervalSeconds,
                    $"{nameof(BotFoundryOptions.SessionCleanupIntervalSeconds)} must be greater than zero.");

            services.AddSingleton(options);

            // Instantiate TelegramBotClient without an explicit using for Telegram.Bot
            var botClient = new global::Telegram.Bot.TelegramBotClient(token);
            services.AddSingleton(botClient);
            services.AddSingleton<global::Telegram.Bot.ITelegramBotClient>(botClient);
            services.AddSingleton<IMessageSender, TelegramMessageSender>();

            if (!services.Any(d => d.ServiceType == typeof(ISessionStore)))
            {
                services.AddSingleton<InMemorySessionStore>();
                services.AddSingleton<ISessionStore>(sp => sp.GetRequiredService<InMemorySessionStore>());
                services.AddHostedService<SessionCleanupService>();
            }

            // Core
            services.AddSingleton<IBotPipeline, Core.BotPipeline>();
            services.AddSingleton<Core.CommandRouter>();
            services.AddSingleton<Core.StepRunner>();

            services.AddHostedService<TelegramPollingAdapter>();
            return services;
        }
    }

    public sealed class BotFoundryOptions
    {
        public TimeSpan SessionExpiry { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// How often, in seconds, expired sessions are removed from the default in-memory session store.
        /// Must be greater than zero. Ignored when a custom <c>ISessionStore</c> is registered.
        /// </summary>
        public int SessionCleanupIntervalSeconds { get; set; } = 60;
    }
}
