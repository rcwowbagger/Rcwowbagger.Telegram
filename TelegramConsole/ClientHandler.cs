using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;

namespace TelegramConsole
{
    public class ClientHandler
    {
        private string _token;
        private TelegramBotClient _client;
        private readonly CancellationTokenSource _cancellationTokenSource;
        public ClientHandler(string token)
        {
            _token = token;
            _client = new TelegramBotClient(token);
            _cancellationTokenSource = new CancellationTokenSource();

            //_client = new TelegramBotClient("2141015782:AAFUH7apC-kuG2E1EmJR9rGAX09OmMzLyGo");
            //var me = await _client.GetMeAsync();
            _client.OnMessage += BotClient_OnMessage;
        }

        private void BotClient_OnMessage(object sender, MessageEventArgs e)
        {
            var chatId = e.Message.Chat.Id;
            var text = e.Message.Text.Trim();

            if (text == "ping")
            {
                Respond(chatId, "pong").GetAwaiter().GetResult();
            }

            Console.WriteLine($"Recevied {e.Message.Text}");

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
