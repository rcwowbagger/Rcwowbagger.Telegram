using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
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
            _client = new TelegramBotClient(_config.Token);
            _cancellationTokenSource = new CancellationTokenSource();
            _logger = Log.ForContext<ClientHandler>();
            _client.OnMessage += BotClient_OnMessage;

            Directory.CreateDirectory(_config.OutputPath);
        }

        private void BotClient_OnMessage(object sender, MessageEventArgs e)
        {
            var chatId = e.Message.Chat.Id;
            var message = e.Message;
            if (message.Type == MessageType.Text)
            {
                var text = message.Text.Trim();

                if (text.ToLowerInvariant() == "ping")
                {
                    Respond(chatId, "pong").GetAwaiter().GetResult();
                }
                _logger.Information("{user}> {message}", message.From?.Username, text);
            }

            if (message.Type == MessageType.Photo)
            {
                var photo = message.Photo.OrderByDescending(x => x.FileSize).First();
                DownloadPhotoAsync(photo).GetAwaiter().GetResult();
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

        public void Start()
        {
            _client.StartReceiving(cancellationToken: _cancellationTokenSource.Token);
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _client.StopReceiving();
        }

    }
}
