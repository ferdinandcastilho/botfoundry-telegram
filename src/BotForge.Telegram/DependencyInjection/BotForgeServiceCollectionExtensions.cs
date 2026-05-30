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
            string token,
            Action<BotForgeOptions>? configure = null)
        {
            var options = new BotForgeOptions();
            configure?.Invoke(options);

            // Instantiate TelegramBotClient without an explicit using for Telegram.Bot
            var botClient = new global::Telegram.Bot.TelegramBotClient(token);
            services.AddSingleton(botClient);
            services.AddSingleton<global::Telegram.Bot.ITelegramBotClient>(botClient);
            services.AddSingleton<IMessageSender, TelegramMessageSender>();

            if (!services.Any(d => d.ServiceType == typeof(ISessionStore)))
                services.AddSingleton<ISessionStore, InMemorySessionStore>();

            // Core
            services.AddSingleton<IBotPipeline, Core.BotPipeline>();
            services.AddSingleton<Core.CommandRouter>();
            services.AddSingleton<Core.StepRunner>();

            services.AddHostedService<TelegramPollingAdapter>();
            return services;
        }
    }

    public sealed class BotForgeOptions
    {
        public TimeSpan SessionExpiry { get; set; } = TimeSpan.FromHours(24);
    }
}
