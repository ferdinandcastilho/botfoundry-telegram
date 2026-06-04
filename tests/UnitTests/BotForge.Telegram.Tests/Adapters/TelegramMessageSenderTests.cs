using BotForge.Telegram.Adapters;
using FluentAssertions;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace BotForge.Telegram.Tests.Adapters
{
    public class TelegramMessageSenderTests
    {
        [Fact]
        public async Task SendTextAsync_WithSimpleText_SendsMessageWithoutMarkup()
        {
            var client = new FakeTelegramBotClient();
            var sender = new TelegramMessageSender(client);

            await sender.SendTextAsync(123, "hello");

            var request = client.SingleRequest<SendMessageRequest>();
            request.Text.Should().Be("hello");
            request.ReplyMarkup.Should().BeNull();
        }

        [Fact]
        public async Task SendTextAsync_WithNullQuickReplies_SendsMessageWithoutMarkup()
        {
            var client = new FakeTelegramBotClient();
            var sender = new TelegramMessageSender(client);

            await sender.SendTextAsync(123, "hello", quickReplies: null);

            client.SingleRequest<SendMessageRequest>().ReplyMarkup.Should().BeNull();
        }

        [Fact]
        public async Task SendTextAsync_WithEmptyQuickReplies_SendsMessageWithoutMarkup()
        {
            var client = new FakeTelegramBotClient();
            var sender = new TelegramMessageSender(client);

            await sender.SendTextAsync(123, "hello", Array.Empty<string>());

            client.SingleRequest<SendMessageRequest>().ReplyMarkup.Should().BeNull();
        }

        [Fact]
        public async Task SendTextAsync_WithQuickReplies_SendsMessageWithReplyKeyboardMarkup()
        {
            var client = new FakeTelegramBotClient();
            var sender = new TelegramMessageSender(client);

            await sender.SendTextAsync(123, "choose", ["A", "B"]);

            var markup = client.SingleRequest<SendMessageRequest>().ReplyMarkup;

            markup.Should().BeOfType<ReplyKeyboardMarkup>();
            var keyboard = (ReplyKeyboardMarkup)markup!;
            keyboard.Keyboard.Should().HaveCount(1);
            keyboard.Keyboard.Single().Select(button => button.Text).Should().Equal("A", "B");
        }

        [Fact]
        public async Task SendTextAsync_WithQuickReplies_ConfiguresKeyboardOptions()
        {
            var client = new FakeTelegramBotClient();
            var sender = new TelegramMessageSender(client);

            await sender.SendTextAsync(123, "choose", ["A"]);

            var keyboard = (ReplyKeyboardMarkup)client.SingleRequest<SendMessageRequest>().ReplyMarkup!;
            keyboard.ResizeKeyboard.Should().BeTrue();
            keyboard.OneTimeKeyboard.Should().BeTrue();
        }

        [Fact]
        public async Task SendTextAsync_PropagatesCancellationToken()
        {
            var client = new FakeTelegramBotClient();
            var sender = new TelegramMessageSender(client);
            using var cts = new CancellationTokenSource();

            await sender.SendTextAsync(123, "hello", cts.Token);

            client.LastCancellationToken.Should().Be(cts.Token);
        }

        private sealed class FakeTelegramBotClient : ITelegramBotClient
        {
            public List<object> Requests { get; } = [];
            public CancellationToken LastCancellationToken { get; private set; }
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

            public TRequest SingleRequest<TRequest>()
            {
                Requests.Should().ContainSingle();
                return Requests.Single().Should().BeOfType<TRequest>().Subject;
            }

            public Task<TResponse> SendRequest<TResponse>(
                IRequest<TResponse> request,
                CancellationToken cancellationToken = default)
            {
                Requests.Add(request);
                LastCancellationToken = cancellationToken;

                if (typeof(TResponse) == typeof(Message))
                    return Task.FromResult((TResponse)(object)new Message());

                throw new NotSupportedException($"Request response type {typeof(TResponse).Name} is not supported by this fake.");
            }

            public Task DownloadFile(TGFile file, Stream destination, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task DownloadFile(string filePath, Stream destination, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task<bool> TestApi(CancellationToken cancellationToken = default)
                => Task.FromResult(true);
        }
    }
}
