namespace Rwowbagger.Telegram
{
    public class TelegramSettings
    {
        public string Token { get; set; }
        public string OutputPath { get; set; }
        public int BatchSendIntervalSec { get; set; } = 2;
        public List<string> PermitCommandsFrom { get; set; }
        public List<long> PermitCommandsFromChatIds { get; set; }
    }
}
