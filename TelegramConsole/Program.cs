using Microsoft.Extensions.Configuration;
using Serilog;
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

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            var appSettings = Configuration.GetSection("AppSettings").Get<AppSettings>();

            var handler = new ClientHandler(appSettings);
            handler.Start();

            Console.ReadLine();

        }
    }
}
