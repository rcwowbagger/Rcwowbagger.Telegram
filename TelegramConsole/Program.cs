using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace TelegramConsole
{
    class Program
    {
        
        static async Task Main(string[] args)
        {
            var Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json",false)
                .AddJsonFile("appsettings.Development.json", true)
                .Build();

            var appSettings = Configuration.GetSection("AppSettings").Get<AppSettings>();

            var handler = new ClientHandler(appSettings.Token);
            handler.Start();

            Console.ReadLine();

        }
    }
}
