using System.Collections.Concurrent;
using BotFoundry.Telegram.Abstractions.Models;
using BotFoundry.Telegram.Abstractions.Ports;

namespace BotFoundry.Telegram.Adapters
{
    internal sealed class InMemorySessionStore : ISessionStore
    {
        private readonly ConcurrentDictionary<long, UserSession> _store = new();

        internal int Count => _store.Count;

        public Task<UserSession?> GetAsync(long chatId, CancellationToken ct = default)
        {
            if (_store.TryGetValue(chatId, out var session))
            {
                if (!session.IsExpired)
                    return Task.FromResult<UserSession?>(session);

                // Drop the expired entry only if it was not replaced meanwhile.
                _store.TryRemove(new KeyValuePair<long, UserSession>(chatId, session));
            }

            return Task.FromResult<UserSession?>(null);
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

        // Removes the sessions of chats that never come back; called by SessionCleanupService.
        internal void RemoveExpired()
        {
            foreach (var entry in _store)
            {
                if (entry.Value.IsExpired)
                    _store.TryRemove(entry);
            }
        }
    }
}
