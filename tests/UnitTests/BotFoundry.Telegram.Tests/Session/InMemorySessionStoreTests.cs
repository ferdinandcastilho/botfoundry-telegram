using BotFoundry.Telegram.Abstractions.Models;
using BotFoundry.Telegram.Adapters;
using FluentAssertions;

namespace BotFoundry.Telegram.Tests.Session
{
    public class InMemorySessionStoreTests
    {
        [Fact]
        public async Task SaveAndGet_ReturnsSameSession()
        {
            var store = new InMemorySessionStore();
            var session = new UserSession { ChatId = 123, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
            session.Data["key"] = "value";

            await store.SaveAsync(session);
            var retrieved = await store.GetAsync(123);

            retrieved.Should().NotBeNull();
            retrieved!.ChatId.Should().Be(123);
            retrieved.Data.Should().ContainKey("key");
            retrieved.Data["key"].Should().Be("value");
        }

        [Fact]
        public async Task Delete_RemovesSession()
        {
            var store = new InMemorySessionStore();
            var session = new UserSession { ChatId = 123, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };

            await store.SaveAsync(session);
            await store.DeleteAsync(123);
            var retrieved = await store.GetAsync(123);

            retrieved.Should().BeNull();
        }

        [Fact]
        public async Task ConcurrentWrites_DoNotCorruptData()
        {
            var store = new InMemorySessionStore();
            var tasks = new List<Task>();

            for (int i = 0; i < 100; i++)
            {
                var index = i;
                var task = Task.Run(async () =>
                {
                    var session = new UserSession { ChatId = index, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
                    session.Data["index"] = index.ToString();
                    await store.SaveAsync(session);
                });
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 100; i++)
            {
                var retrieved = await store.GetAsync(i);
                retrieved.Should().NotBeNull();
                retrieved!.ChatId.Should().Be(i);
                retrieved.Data.Should().ContainKey("index");
                retrieved.Data["index"].Should().Be(i.ToString());
            }
        }

        [Fact]
        public async Task Get_WhenSessionIsExpired_ReturnsNullAndRemovesIt()
        {
            var store = new InMemorySessionStore();
            await store.SaveAsync(new UserSession { ChatId = 123, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) });

            var retrieved = await store.GetAsync(123);

            retrieved.Should().BeNull();
            (await store.GetAsync(123)).Should().BeNull();
        }

        [Fact]
        public async Task RemoveExpired_RemovesOnlyExpiredSessions()
        {
            var store = new InMemorySessionStore();
            await store.SaveAsync(new UserSession { ChatId = 1, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) });
            await store.SaveAsync(new UserSession { ChatId = 2, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });

            store.RemoveExpired();

            store.Count.Should().Be(1);
            (await store.GetAsync(2)).Should().NotBeNull();
        }
    }
}
