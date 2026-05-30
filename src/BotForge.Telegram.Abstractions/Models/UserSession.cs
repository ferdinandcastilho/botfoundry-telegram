using System;
using System.Collections.Generic;

namespace BotForge.Telegram.Abstractions.Models
{
    public sealed class UserSession
    {
        public required long ChatId { get; init; }
        public string? ActiveTopic { get; set; }
        public string? ActiveStepId { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public Dictionary<string, string> Data { get; } = new();
        public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
        public bool HasActiveFlow => ActiveTopic is not null;
        public void ClearFlow() { ActiveTopic = null; ActiveStepId = null; Data.Clear(); }
    }
}
