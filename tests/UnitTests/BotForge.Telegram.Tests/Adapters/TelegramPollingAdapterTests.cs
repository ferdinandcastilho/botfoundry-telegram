using System.Reflection;
using BotForge.Telegram.Abstractions.Models;
using BotForge.Telegram.Abstractions.Ports;
using BotForge.Telegram.Adapters;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace BotForge.Telegram.Tests.Adapters
{
    public class TelegramPollingAdapterTests
    {
        [Fact]
        public async Task StartAsync_WithCancellationRequested_CompletesWithoutNetworkAccess()
        {
            var adapter = CreateAdapter(new FakeBotPipeline(), new FakeSessionStore());
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await adapter.StartAsync(cts.Token);
        }

        [Fact]
        public async Task StopAsync_Completes()
        {
            var adapter = CreateAdapter(new FakeBotPipeline(), new FakeSessionStore());

            await adapter.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task HandleUpdateAsync_WhenUpdateHasNoText_DoesNotInvokePipeline()
        {
            var pipeline = new FakeBotPipeline();
            var sessionStore = new FakeSessionStore();
            var adapter = CreateAdapter(pipeline, sessionStore);

            await InvokeHandleUpdateAsync(adapter, new Update { Message = new Message { Chat = new Chat { Id = 123 } } });

            pipeline.ProcessedContexts.Should().BeEmpty();
        }

        [Fact]
        public async Task HandleUpdateAsync_WhenSessionExists_UsesStoredSession()
        {
            var existingSession = new UserSession { ChatId = 123, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
            var pipeline = new FakeBotPipeline();
            var sessionStore = new FakeSessionStore { SessionToReturn = existingSession };
            var adapter = CreateAdapter(pipeline, sessionStore);

            await InvokeHandleUpdateAsync(adapter, TextUpdate(123, "hello"));

            pipeline.SingleContext().Session.Should().BeSameAs(existingSession);
        }

        [Fact]
        public async Task HandleUpdateAsync_WhenSessionDoesNotExist_CreatesSessionExpiringInTwentyFourHours()
        {
            var before = DateTimeOffset.UtcNow;
            var pipeline = new FakeBotPipeline();
            var sessionStore = new FakeSessionStore();
            var adapter = CreateAdapter(pipeline, sessionStore);

            await InvokeHandleUpdateAsync(adapter, TextUpdate(123, "hello"));

            var session = pipeline.SingleContext().Session;
            session.ChatId.Should().Be(123);
            session.ExpiresAt.Should().BeOnOrAfter(before.AddHours(24));
            session.ExpiresAt.Should().BeOnOrBefore(DateTimeOffset.UtcNow.AddHours(24));
        }

        [Fact]
        public async Task HandleUpdateAsync_WhenSessionIsNotExpired_SavesSession()
        {
            var pipeline = new FakeBotPipeline();
            var sessionStore = new FakeSessionStore();
            var adapter = CreateAdapter(pipeline, sessionStore);

            await InvokeHandleUpdateAsync(adapter, TextUpdate(123, "hello"));

            sessionStore.SavedSessions.Should().ContainSingle();
            sessionStore.SavedSessions.Single().ChatId.Should().Be(123);
            sessionStore.DeletedChatIds.Should().BeEmpty();
        }

        [Fact]
        public async Task HandleUpdateAsync_WhenSessionIsExpiredAfterProcessing_DeletesSession()
        {
            var pipeline = new FakeBotPipeline
            {
                OnProcess = context =>
                    context.Session.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1)
            };
            var sessionStore = new FakeSessionStore();
            var adapter = CreateAdapter(pipeline, sessionStore);

            await InvokeHandleUpdateAsync(adapter, TextUpdate(123, "hello"));

            sessionStore.DeletedChatIds.Should().ContainSingle().Which.Should().Be(123);
            sessionStore.SavedSessions.Should().BeEmpty();
        }

        [Fact]
        public async Task HandleUpdateAsync_PropagatesCancellationTokenToPipelineAndStore()
        {
            var pipeline = new FakeBotPipeline();
            var sessionStore = new FakeSessionStore();
            var adapter = CreateAdapter(pipeline, sessionStore);
            using var cts = new CancellationTokenSource();

            await InvokeHandleUpdateAsync(adapter, TextUpdate(123, "hello"), cts.Token);

            pipeline.LastCancellationToken.Should().Be(cts.Token);
            sessionStore.GetCancellationToken.Should().Be(cts.Token);
            sessionStore.SaveCancellationToken.Should().Be(cts.Token);
        }

        [Fact]
        public async Task HandleErrorAsync_WhenPollingFails_DoesNotPropagateException()
        {
            var adapter = CreateAdapter(new FakeBotPipeline(), new FakeSessionStore());
            var act = () => InvokeHandleErrorAsync(adapter, new InvalidOperationException("polling failed"));

            await act.Should().NotThrowAsync();
        }

        private static TelegramPollingAdapter CreateAdapter(
            FakeBotPipeline pipeline,
            FakeSessionStore sessionStore)
            => new(
                new FakeTelegramBotClient(),
                pipeline,
                new FakeMessageSender(),
                sessionStore,
                NullLogger<TelegramPollingAdapter>.Instance);

        private static Update TextUpdate(long chatId, string text)
            => new() { Message = new Message { Chat = new Chat { Id = chatId }, Text = text } };

        private static async Task InvokeHandleUpdateAsync(
            TelegramPollingAdapter adapter,
            Update update,
            CancellationToken cancellationToken = default)
        {
            var method = typeof(TelegramPollingAdapter).GetMethod(
                "HandleUpdateAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);

            method.Should().NotBeNull();
            var task = (Task)method!.Invoke(adapter, [new FakeTelegramBotClient(), update, cancellationToken])!;
            await task;
        }

        private static async Task InvokeHandleErrorAsync(
            TelegramPollingAdapter adapter,
            Exception exception,
            CancellationToken cancellationToken = default)
        {
            var method = typeof(TelegramPollingAdapter).GetMethod(
                "HandleErrorAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);

            method.Should().NotBeNull();
            var task = (Task)method!.Invoke(adapter, [new FakeTelegramBotClient(), exception, cancellationToken])!;
            await task;
        }

        private sealed class FakeBotPipeline : IBotPipeline
        {
            public List<BotContext> ProcessedContexts { get; } = [];
            public CancellationToken LastCancellationToken { get; private set; }
            public Action<BotContext>? OnProcess { get; init; }

            public BotContext SingleContext()
            {
                ProcessedContexts.Should().ContainSingle();
                return ProcessedContexts.Single();
            }

            public Task ProcessAsync(BotContext context, CancellationToken ct = default)
            {
                ProcessedContexts.Add(context);
                LastCancellationToken = ct;
                OnProcess?.Invoke(context);
                return Task.CompletedTask;
            }
        }

        private sealed class FakeSessionStore : ISessionStore
        {
            public UserSession? SessionToReturn { get; init; }
            public List<UserSession> SavedSessions { get; } = [];
            public List<long> DeletedChatIds { get; } = [];
            public CancellationToken GetCancellationToken { get; private set; }
            public CancellationToken SaveCancellationToken { get; private set; }
            public CancellationToken DeleteCancellationToken { get; private set; }

            public Task<UserSession?> GetAsync(long chatId, CancellationToken ct = default)
            {
                GetCancellationToken = ct;
                return Task.FromResult(SessionToReturn);
            }

            public Task SaveAsync(UserSession session, CancellationToken ct = default)
            {
                SaveCancellationToken = ct;
                SavedSessions.Add(session);
                return Task.CompletedTask;
            }

            public Task DeleteAsync(long chatId, CancellationToken ct = default)
            {
                DeleteCancellationToken = ct;
                DeletedChatIds.Add(chatId);
                return Task.CompletedTask;
            }
        }

        private sealed class FakeMessageSender : IMessageSender
        {
            public Task SendTextAsync(long chatId, string text, CancellationToken ct = default)
                => Task.CompletedTask;

            public Task SendTextAsync(
                long chatId,
                string text,
                IReadOnlyList<string>? quickReplies,
                CancellationToken ct = default)
                => Task.CompletedTask;
        }

        private sealed class FakeTelegramBotClient : ITelegramBotClient
        {
            public TimeSpan Timeout { get; set; }
            public IExceptionParser ExceptionsParser { get; set; } = new DefaultExceptionParser();
            public long BotId => 42;
            public bool LocalBotServer => false;

            public event AsyncEventHandler<ApiRequestEventArgs>? OnMakingApiRequest
            {
                add { }
                remove { }
            }

            public event AsyncEventHandler<ApiResponseEventArgs>? OnApiResponseReceived
            {
                add { }
                remove { }
            }

            public Task<TResponse> SendRequest<TResponse>(
                IRequest<TResponse> request,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task DownloadFile(TGFile file, Stream destination, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task DownloadFile(string filePath, Stream destination, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task<bool> TestApi(CancellationToken cancellationToken = default)
                => Task.FromResult(true);
        }
    }
}
