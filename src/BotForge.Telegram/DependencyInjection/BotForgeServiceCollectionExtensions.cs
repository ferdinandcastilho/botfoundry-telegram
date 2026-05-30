using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using BotForge.Telegram.Abstractions.Ports;
using BotForge.Telegram.Adapters;

namespace BotForge.Telegram.DependencyInjection
{
    public static class BotForgeServiceCollectionExtensions
    {
        public static IServiceCollection AddBotForge(
            this IServiceCollection services,
            Action<BotForgeOptions> configure)
        {
            var options = new BotForgeOptions { Token = string.Empty };
            configure(options);

            // Instantiate TelegramBotClient without an explicit using for Telegram.Bot
            services.AddSingleton<global::Telegram.Bot.ITelegramBotClient>(_ => new global::Telegram.Bot.TelegramBotClient(options.Token));
            services.AddSingleton<IMessageSender, TelegramMessageSender>();

            if (!services.Any(d => d.ServiceType == typeof(ISessionStore)))
                services.AddSingleton<ISessionStore, InMemorySessionStore>();

            // IBotPipeline registration left commented — BotPipeline will be implemented in a later task
            // services.AddSingleton<IBotPipeline, BotPipeline>();

            services.AddHostedService<TelegramPollingAdapter>();
            return services;
        }
    }

    public sealed class BotForgeOptions
    {
        public required string Token { get; set; }
        public TimeSpan SessionExpiry { get; set; } = TimeSpan.FromHours(24);
    }
}
