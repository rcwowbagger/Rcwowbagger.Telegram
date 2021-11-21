using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramConsole
{
    public class ClientHandler
    {
        private readonly AppSettings _config;
        private TelegramBotClient _client;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ILogger _logger;

        public ClientHandler(AppSettings config)
        {
            _config = config;
            _cancellationTokenSource = new CancellationTokenSource();
            _logger = Log.ForContext<ClientHandler>();
            _client = new TelegramBotClient(_config.Token);
            _client.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                new ReceiverOptions { AllowedUpdates = { } },
                cancellationToken: _cancellationTokenSource.Token);

            var me = _client.GetMeAsync().GetAwaiter().GetResult();
            _logger.Information($"Notifications configured for @{me.Username}");

            Directory.CreateDirectory(_config.OutputPath);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken arg3)
        {
            var chatId = update.Message.Chat.Id;
            var message = update.Message;
            if (message.Type == MessageType.Text)
            {
                var text = message.Text.Trim();

                if (text.ToLowerInvariant() == "ping")
                {
                    Respond(chatId, "pong").GetAwaiter().GetResult();
                }

                if (text.ToLowerInvariant() == "chuck")
                {
                    var chuck = GetChuckJoke().GetAwaiter().GetResult();
                    Respond(chatId, chuck).GetAwaiter().GetResult();
                }

                _logger.Information("{user}> {message}", message.From?.Username, text);
            }

            if (message.Type == MessageType.Photo)
            {
                var photo = message.Photo.OrderByDescending(x => x.FileSize).First();
                DownloadPhotoAsync(photo).GetAwaiter().GetResult();
            }
        }

        private async Task HandleErrorAsync(ITelegramBotClient arg1, Exception ex, CancellationToken arg3)
        {
            _logger.Error(ex, "Notification Bot produced an error:");
        }

        private async Task<string> GetChuckJoke()
        {
            try
            {
                var httpClient = new HttpClient()
                {
                    BaseAddress = new Uri("https://api.chucknorris.io")
                };

                var response = await httpClient.GetAsync("jokes/random");
                var contents = await response.Content.ReadAsStringAsync();

                var jObject = JObject.Parse(contents);
                var joke = jObject.Value<string>("value");

                return joke;
            }
            catch(Exception ex)
            {
                _logger.Error(ex, "");
                throw;
            }
        }

        private async Task DownloadPhotoAsync(Telegram.Bot.Types.PhotoSize photo)
        {
            try
            {
                using (var fileStream = new FileStream(Path.Combine(_config.OutputPath, $"{photo.FileUniqueId}.jpg"), FileMode.CreateNew, FileAccess.Write))
                {
                    await _client.GetInfoAndDownloadFileAsync(photo.FileId, fileStream, _cancellationTokenSource.Token);
                }
            }
            catch(Exception ex)
            {
                _logger.Error(ex, "Error downloading photo"); 
            }
        }

        private async Task Respond(long chatId, string message)
        {
            await _client.SendTextMessageAsync(chatId, message);
        }


        public void Stop()
        {
            _cancellationTokenSource.Cancel();
        }

    }
}
