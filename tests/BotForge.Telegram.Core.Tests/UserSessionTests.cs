using BotForge.Telegram.Abstractions.Models;
using FluentAssertions;

namespace BotForge.Telegram.Core.Tests;

public sealed class UserSessionTests
{
    [Fact]
    public void HasActiveFlow_ActiveTopicNull_ReturnsFalse()
    {
        var session = new UserSession { ChatId = 123 };

        session.HasActiveFlow.Should().BeFalse();
    }

    [Fact]
    public void HasActiveFlow_ActiveTopicSet_ReturnsTrue()
    {
        var session = new UserSession { ChatId = 123, ActiveTopic = "start" };

        session.HasActiveFlow.Should().BeTrue();
    }

    [Fact]
    public void ClearFlow_ClearsTopicStepIdAndDataAtomically()
    {
        var session = new UserSession
        {
            ChatId = 123,
            ActiveTopic = "start",
            ActiveStepId = "step1"
        };
        session.Data["key"] = "value";

        session.ClearFlow();

        session.ActiveTopic.Should().BeNull();
        session.ActiveStepId.Should().BeNull();
        session.Data.Should().BeEmpty();
    }

    [Fact]
    public void IsExpired_ExpiresAtInPast_ReturnsTrue()
    {
        var session = new UserSession { ChatId = 123, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) };

        session.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_ExpiresAtInFuture_ReturnsFalse()
    {
        var session = new UserSession { ChatId = 123, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1) };

        session.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void HasActiveFlow_DoesNotDependOnActiveStepId()
    {
        var session = new UserSession { ChatId = 123, ActiveStepId = "step1" };

        session.HasActiveFlow.Should().BeFalse();
    }

    [Fact]
    public void ClearFlow_PreservesChatIdAndExpiresAt()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
        var session = new UserSession
        {
            ChatId = 123,
            ExpiresAt = expiresAt,
            ActiveTopic = "start",
            ActiveStepId = "step1"
        };

        session.ClearFlow();

        session.ChatId.Should().Be(123);
        session.ExpiresAt.Should().Be(expiresAt);
    }
}
