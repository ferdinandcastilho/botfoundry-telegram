using System.Collections.Concurrent;
using BotForge.Telegram.Abstractions.Models;
using BotForge.Telegram.Abstractions.Ports;

namespace BotForge.Telegram.Adapters
{
    internal sealed class InMemorySessionStore : ISessionStore
    {
        private readonly ConcurrentDictionary<long, UserSession> _store = new();

        public Task<UserSession?> GetAsync(long chatId, CancellationToken ct = default)
        {
            _store.TryGetValue(chatId, out var session);
            return Task.FromResult(session);
        }

        public Task SaveAsync(UserSession session, CancellationToken ct = default)
        {
            _store[session.ChatId] = session;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(long chatId, CancellationToken ct = default)
        {
            _store.TryRemove(chatId, out _);
            return Task.CompletedTask;
        }
    }
}
