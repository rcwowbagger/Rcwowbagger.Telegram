using Serilog;
using System.Collections.Concurrent;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Rwowbagger.Telegram
{
    public class TelegramClient
    {
        private TelegramBotClient _client;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private TelegramBotClient _clientPolling;
        private TelegramBotClient _clientSending;
        private readonly ILogger _logger;
        private readonly TelegramSettings _settings;
        public event Action<string, string> OnUpdate;
        public event Action<string, string> OnError;
        private readonly BlockingCollection<(long chatId, string message, ParseMode parseMode)> _sendQueue = new();

        public Dictionary<string, (Func<string, string, string> callbackFunc, ParseMode parseMode)> MenuItemsFunc { get; }
        public Dictionary<string, Action<string, string>> MenuItemsAction { get; }
        public Dictionary<string, (string message, Func<string, string> callback, List<string> options)> InlineItemsFunc { get; }

        public TelegramClient(TelegramSettings setting)
        {
            _settings = setting;
            _logger = Log.ForContext<TelegramClient>();
            MenuItemsFunc = new Dictionary<string, (Func<string, string, string>, ParseMode parseMode)>();
            MenuItemsAction = new Dictionary<string, Action<string, string>>();
            InlineItemsFunc = new Dictionary<string, (string, Func<string, string>, List<string>)>();
            _clientPolling = new TelegramBotClient(_settings.Token);
            _clientSending = new TelegramBotClient(_settings.Token);
        }

        public async Task BeginReceiveAsync(CancellationToken cancellationToken)
        {
            Task.Run(async () => await ReadFromCollection(cancellationToken));
            _logger.Information("Beginning Receive");

            _clientPolling.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                new ReceiverOptions { AllowedUpdates = { }, ThrowPendingUpdates = true },
                cancellationToken: cancellationToken);
        }

        public async Task SendMessageAsync(string recipient, string message)
        {
            await SendMessageAsync(recipient, message, 0);
        }

        public async Task SendMessageAsync(string recipient, string message, Int16 parseMode = 0)
        {
            if (!Int64.TryParse(recipient, out var chatId))
            {
                throw new ArgumentException($"TelegramBotService expects Int64 as recipient: got {recipient}");
            }
            _sendQueue.Add((chatId, message, (ParseMode)parseMode));
        }

        public void AddCallback(string tag, Func<string, string, string> callbackFunction, Int16 parseMode = 0)
        {
            MenuItemsFunc.Add(tag.Trim().ToLowerInvariant(), (callbackFunction, (ParseMode)parseMode));
        }

        public void AddCallback(string tag, Action<string, string> callbackFunction, Int16 parseMode = 0)
        {
            MenuItemsAction.Add(tag.Trim().ToLowerInvariant(), callbackFunction);
        }

        public void AddCallbackWithExpectedResponse(string tag, string message, Func<string, string> callbackFunction, List<string> options)
        {
            tag = tag.Trim().ToLowerInvariant();
            InlineItemsFunc.Add(tag.Trim().ToLowerInvariant(), (message, callbackFunction, options));

            MenuItemsAction.Add(tag.Trim().ToLowerInvariant(), async (id, value) =>
            {
                await _clientSending.SendTextMessageAsync(chatId: id, text: message, replyMarkup: await InlineMarkup(tag, options));
            });
        }

        private async Task HandleErrorAsync(ITelegramBotClient arg1, Exception ex, CancellationToken arg3)
        {
            _logger.Warning(ex, "Bot error");
        }

        private async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken cancellationToken)
        {
            _logger.Verbose("Got Update");

            Chat? chat = null;
            if (update.Message != null
                && _settings.PermitCommandsFrom.Contains(update.Message?.From?.Username)
            )
            {
                _logger.Information("{text} from {user} ({id})", update.Message.Text, update.Message.From, update.Message.Chat.Id);
                var message = update.Message.Text;
                chat = update.Message.Chat;

                //Is update from authorised chat
                if (_settings.PermitCommandsFromChatIds.Any() && _settings.PermitCommandsFromChatIds.Contains(chat.Id))
                {
                    await MapResponse(message?.Trim().ToLowerInvariant(), client, chat, update, cancellationToken);
                }
            }
            else if (update.MyChatMember?.Chat != null)
            {
                return;
            }
            else if (update.Type == UpdateType.CallbackQuery)
            {
                chat = update.CallbackQuery.Message.Chat;
                //Is update from authorised chat
                if (_settings.PermitCommandsFromChatIds.Any() && _settings.PermitCommandsFromChatIds.Contains(chat.Id))
                {
                    _logger.Verbose("Got Callback request");
                    var value = update.CallbackQuery.Data?.Split(":");
                    if (value.Length > 1)
                    {
                        try
                        {
                            if (update.CallbackQuery.Message != null)
                            {
                                await _clientSending.DeleteMessageAsync(chat, update.CallbackQuery.Message.MessageId, cancellationToken);
                            }

                            var command = value[0];
                            var data = value[1];
                            var callback = InlineItemsFunc[command];
                            var callbackResponse = callback.callback(data);

                            await _clientSending.SendTextMessageAsync(
                                chatId: chat.Id,
                                text: callbackResponse,
                                replyMarkup: await GetMarkup(),
                                cancellationToken: cancellationToken
                                );
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "");


                            await _clientSending.SendTextMessageAsync(
                               chatId: chat.Id,
                               text: "An Error occurred",
                               replyMarkup: await GetMarkup(),
                               cancellationToken: cancellationToken
                               );
                        }
                    }
                }
            }
            else
            {
                _logger.Warning("Unhandled Message type");
                return;
            }

        }

        private async Task MapResponse(string message, ITelegramBotClient client, Chat? chat, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (chat == null)
                {
                    return;
                }

                if (message == "menu")
                {
                    var response = await _clientPolling.SendTextMessageAsync(
                            chatId: chat.Id,
                            text: "Menu",
                            replyMarkup: await GetMarkup(),
                            cancellationToken: cancellationToken);
                }
                else if (MenuItemsFunc.ContainsKey(message))
                {
                    if (MenuItemsFunc.ContainsKey(message))
                    {
                        var response = MenuItemsFunc[message].callbackFunc?.Invoke(chat.Id.ToString(), message) ?? String.Empty;

                        await client.SendTextMessageAsync(
                                chatId: chat.Id,
                                text: response,
                                parseMode: MenuItemsFunc[message].parseMode == 0 ? null : MenuItemsFunc[message].parseMode,
                                replyMarkup: await GetMarkup(),
                                cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await client.SendTextMessageAsync(
                                chatId: chat.Id,
                                text: "No Response",
                                parseMode: null,
                                replyMarkup: await GetMarkup(),
                                cancellationToken: cancellationToken);
                    }
                }
                else if (MenuItemsAction.ContainsKey(message))
                {
                    await client.SendTextMessageAsync(
                            chatId: chat.Id,
                            text: $"Requested {message}",
                            replyMarkup: await GetMarkup(),
                            cancellationToken: cancellationToken);

                    MenuItemsAction[message]?.Invoke(chat.Id.ToString(), message);
                }
                else if (InlineItemsFunc.ContainsKey(message))
                {
                    var callbackItem = InlineItemsFunc[message];
                    await client.SendTextMessageAsync(
                            chatId: chat.Id,
                            text: callbackItem.message,
                            replyMarkup: await InlineMarkup(callbackItem.message, callbackItem.options),
                            replyToMessageId: update.Message?.MessageId,
                            cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error handling update");
            }
        }

        private async Task<ReplyKeyboardMarkup> GetMarkup()
        {
            var allButtons = new List<KeyboardButton>();

            allButtons.AddRange(MenuItemsFunc.Keys.Select(key => new KeyboardButton(key)).ToList());
            allButtons.AddRange(MenuItemsAction.Keys.Select(key => new KeyboardButton(key)).ToList());

            var keyBoard = new List<List<KeyboardButton>>();


            for (int i = 0; i < allButtons.Count; i++)
            {
                int row = i / 4;
                if (keyBoard.Count() < row + 1)
                {
                    keyBoard.Add(new List<KeyboardButton>());
                }
                keyBoard[row].Add(allButtons[i]);
            }
            ReplyKeyboardMarkup replyKeyboardMarkup = new ReplyKeyboardMarkup(keyBoard)
            {
                ResizeKeyboard = true
            };
            return replyKeyboardMarkup;
        }

        private async Task ReadFromCollection(CancellationToken cancellationToken)
        {
            var sendBuffer = new Dictionary<long, StringBuilder>();
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    //blocking wait for message
                    (long chatId, string message, ParseMode parseMode) queueItem = _sendQueue.Take(cancellationToken);
                    if (queueItem.parseMode == 0)
                    {
                        ProcessQueueItem(sendBuffer, queueItem);

                        //get all messages in queue
                        while (_sendQueue.TryTake(out _))
                        {
                            var nextItem = _sendQueue.Take(cancellationToken);
                            ProcessQueueItem(sendBuffer, nextItem);
                        }
                        // send buffer contents

                        await Parallel.ForEachAsync(sendBuffer.ToList(), async (item, token) =>
                        {
                            if (item.Value.Length == 0)
                            {
                                return;
                            }

                            await Send(chatId: item.Key, message: item.Value.ToString(), parseMode: queueItem.parseMode);

                            //await Send(chatId: item.Key, message: item.Value.ToString(), parseMode: 0);
                            item.Value.Clear();
                        });

                        await Task.Delay((int)TimeSpan.FromSeconds(_settings.BatchSendIntervalSec).TotalMilliseconds);
                    }
                    else
                    {
                        await Send(chatId: queueItem.chatId, message: queueItem.message, parseMode: queueItem.parseMode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Error in send message");
                }
            }
        }

        private static void ProcessQueueItem(Dictionary<long, StringBuilder> buffer, (long chatId, string message, ParseMode parseMode) queueItem)
        {
            buffer.TryAdd(queueItem.chatId, new StringBuilder());
            buffer[queueItem.chatId].AppendLine(queueItem.message);
        }

        private async Task Send(long chatId, string message, ParseMode parseMode = 0)
        {
            _logger.Debug("Sending to {chatId}", chatId);
            var response = await _clientSending.SendTextMessageAsync(
                chatId: chatId,
                text: message,
                parseMode: parseMode == 0 ? null : parseMode
                );
        }

        private async Task<InlineKeyboardMarkup> InlineMarkup(string command, List<string> options)
        {
            InlineKeyboardMarkup inlineKeyboard = new(
                options.Select(x => InlineKeyboardButton.WithCallbackData(text: x, callbackData: $"{command}:{x}")).ToArray()
                );

            return inlineKeyboard;
        }

        public void AddCallbackParam(string tag, Func<string, string, List<string>, string> callbackFunction, short parseMode = 0)
        {
            throw new NotImplementedException();
        }

        public void AddCallbackArithmeticParam(string tag, Func<string, string, List<int>, string> callbackFunction)
        {
            throw new NotImplementedException();
        }


    }
}
