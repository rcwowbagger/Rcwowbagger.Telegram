using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramConsole
{
    class Program
    {
        
        static async Task Main(string[] args)
        {
            var Configuration = new ConfigurationBuilder()
                .

            _client = new TelegramBotClient("2141015782:AAFUH7apC-kuG2E1EmJR9rGAX09OmMzLyGo");
            var me = await _client.GetMeAsync();
            Console.WriteLine($"Hello, World! I am user {me.Id} and my name is {me.FirstName}.");
            using var cts = new CancellationTokenSource();

            _client.OnMessage += BotClient_OnMessage;

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            _client.StartReceiving(cancellationToken: cts.Token);
                //new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync),
                //cts.Token);

            Console.WriteLine($"Start listening for @{me.Username}");
            Console.ReadLine();

        }
    }
}
