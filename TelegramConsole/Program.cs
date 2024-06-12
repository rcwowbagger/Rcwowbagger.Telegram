using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rwowbagger.Telegram;
using Serilog;
using System;
using System.Reflection;
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
                .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .Enrich.FromLogContext()
                .MinimumLevel.Debug()
                .CreateLogger();

            //var appSettings = Configuration.GetSection("Telegram").Get<TelegramSettings>();

            //var handler = new ClientHandler(appSettings);

            try
            {
                await CreateHostBuilder(args, Configuration).Build().RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }

        }

        public static IHostBuilder CreateHostBuilder(string[] args, IConfigurationRoot configuration) =>
           Host.CreateDefaultBuilder(args)
               .ConfigureServices(services =>
               {
                   services.AddScoped<TelegramSettings>(x => configuration.GetRequiredSection("Telegram").Get<TelegramSettings>());
                   services.AddSingleton<TelegramClient>();
                   services.AddHostedService<TestService>();
               })
           ;
    }
}
