using BotForge.Telegram.Abstractions.Models;
using BotForge.Telegram.Abstractions.Ports;

namespace BotForge.Telegram.Tests.Fakes
{
    internal sealed class FakeSessionStore : ISessionStore
    {
        private readonly Dictionary<long, UserSession> _store = [];

        public Task<UserSession?> GetAsync(long chatId, CancellationToken ct = default)
            => Task.FromResult(_store.GetValueOrDefault(chatId));

        public Task SaveAsync(UserSession session, CancellationToken ct = default)
        {
            _store[session.ChatId] = session;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(long chatId, CancellationToken ct = default)
        {
            _store.Remove(chatId);
            return Task.CompletedTask;
        }
    }
}
