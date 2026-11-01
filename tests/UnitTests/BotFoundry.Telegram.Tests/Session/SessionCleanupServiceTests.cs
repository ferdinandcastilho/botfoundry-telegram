using BotFoundry.Telegram.Abstractions.Models;
using BotFoundry.Telegram.Abstractions.Ports;
using BotFoundry.Telegram.Adapters;
using BotFoundry.Telegram.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BotFoundry.Telegram.Tests.Session
{
    public class SessionCleanupServiceTests
    {
        [Fact]
        public async Task ExecuteAsync_RemovesExpiredSessionsInBackground()
        {
            var store = new InMemorySessionStore();
            await store.SaveAsync(new UserSession { ChatId = 1, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) });
            await store.SaveAsync(new UserSession { ChatId = 2, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
            var service = new SessionCleanupService(store, TimeSpan.FromMilliseconds(10));

            await service.StartAsync(CancellationToken.None);
            try
            {
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (store.Count > 1 && DateTime.UtcNow < deadline)
                    await Task.Delay(20);
            }
            finally
            {
                await service.StopAsync(CancellationToken.None);
            }

            store.Count.Should().Be(1);
        }

        [Fact]
        public async Task StopAsync_WhenRunning_CompletesWithoutThrowing()
        {
            var service = new SessionCleanupService(new InMemorySessionStore(), TimeSpan.FromMinutes(1));
            await service.StartAsync(CancellationToken.None);

            var act = () => service.StopAsync(CancellationToken.None);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public void AddBotFoundry_WithDefaultStore_RegistersTheCleanupService()
        {
            var services = new ServiceCollection();

            services.AddBotFoundry("123456:TEST");

            services.Should().Contain(d => d.ServiceType == typeof(IHostedService)
                                           && d.ImplementationType == typeof(SessionCleanupService));
            services.Should().Contain(d => d.ServiceType == typeof(ISessionStore));
        }

        [Fact]
        public void AddBotFoundry_WithDefaultStore_CleanupServiceAndSessionStoreShareTheSameInstance()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddBotFoundry("123456:TEST");
            using var provider = services.BuildServiceProvider();

            var store = provider.GetRequiredService<ISessionStore>();
            var concrete = provider.GetRequiredService<InMemorySessionStore>();
            var hosted = provider.GetServices<IHostedService>().OfType<SessionCleanupService>();

            store.Should().BeSameAs(concrete);
            hosted.Should().ContainSingle();
        }

        [Fact]
        public async Task AddBotFoundry_UsesConfiguredCleanupIntervalInSeconds()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddBotFoundry("123456:TEST", o => o.SessionCleanupIntervalSeconds = 1);
            using var provider = services.BuildServiceProvider();
            var store = provider.GetRequiredService<InMemorySessionStore>();
            await store.SaveAsync(new UserSession { ChatId = 1, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) });
            var service = provider.GetServices<IHostedService>().OfType<SessionCleanupService>().Single();

            await service.StartAsync(CancellationToken.None);
            try
            {
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (store.Count > 0 && DateTime.UtcNow < deadline)
                    await Task.Delay(50);
            }
            finally
            {
                await service.StopAsync(CancellationToken.None);
            }

            store.Count.Should().Be(0);
        }

        [Fact]
        public void BotFoundryOptions_CleanupIntervalDefaultsToSixtySeconds()
        {
            new BotFoundryOptions().SessionCleanupIntervalSeconds.Should().Be(60);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void AddBotFoundry_WithNonPositiveCleanupInterval_Throws(int seconds)
        {
            var services = new ServiceCollection();

            var act = () => services.AddBotFoundry("123456:TEST", o => o.SessionCleanupIntervalSeconds = seconds);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void AddBotFoundry_WithCustomStore_DoesNotRegisterTheCleanupService()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISessionStore, InMemorySessionStore>();

            services.AddBotFoundry("123456:TEST");

            services.Should().NotContain(d => d.ImplementationType == typeof(SessionCleanupService));
        }
    }
}
