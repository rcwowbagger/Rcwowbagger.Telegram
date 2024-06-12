using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Rwowbagger.Telegram;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types.Enums;

namespace TelegramConsole
{
    public class TestService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly TelegramClient _telegramBotService;
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private readonly TelegramSettings _settings;

        public TestService(TelegramSettings settings, TelegramClient client)
        {
            _logger = Log.ForContext<TestService>();
            _settings = settings;
            _telegramBotService = client;
            _telegramBotService.AddCallback("/chuck", (id, tag) => GetChuckJoke().GetAwaiter().GetResult());
            _telegramBotService.AddCallback("/ping", (id, tag) => "pong");
            _telegramBotService.AddCallback("/MD", (id, tag) => GetMarkDown(), parseMode: (Int16)ParseMode.MarkdownV2);
            _telegramBotService.AddCallbackWithExpectedResponse("/buffer", "Set buffer %", SetValue, ["1", "2", "3", "4"]);
        }

        private string GetMarkDown()
        {
            var sb = new StringBuilder().Append("```\n");
            sb.AppendLine("stuff\n");
            sb.Append("```");
            return sb.ToString();
        }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _settings.PermitCommandsFromChatIds.ToList().ForEach(async (recipient) =>
            {
                await _telegramBotService.SendMessageAsync(recipient.ToString(), "``` Starting Up ```", (Int16)ParseMode.MarkdownV2);
            });

            _logger.Information("Starting up");
            await _telegramBotService.BeginReceiveAsync(stoppingToken);
        }

        protected string SetValue(string value)
        {
            return $"Value Set {value}";
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

                var joke = JsonSerializer.Deserialize<Joke>(contents);

                return joke.value;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "");
                throw;
            }
        }

        private async Task<string> BeginTest(string chatIdString, string tag, CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await _telegramBotService.SendMessageAsync(chatIdString, $"{DateTime.UtcNow:o}");
                    await Task.Delay(1_000);
                }
            }, cancellationToken);

            return "Running";
        }


    }
}
