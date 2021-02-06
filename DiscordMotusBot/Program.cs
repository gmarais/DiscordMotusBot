using System;
using Discord;
using Discord.WebSocket;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DiscordMotusBot
{
    class BotConfig
    {
        public string key = null;
        public Dictionary<ulong, GameConfig> games_configs = null;
    }

    internal class Program
    {
        private BotConfig _botConfig;
        private Dictionary<ulong, MotusGame> _motusGames = new Dictionary<ulong, MotusGame>();
        private MotusMessagesFormulator _motusMessagesFormulator = new MotusMessagesFormulator();
        private DiscordSocketClient _client;

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient();
            _client.Log += Log;
            _client.MessageReceived += MotusGame;

            _botConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<BotConfig>(File.ReadAllText("BotConfig.json"));
            await _client.LoginAsync(TokenType.Bot, _botConfig.key);
            await _client.StartAsync();
            await Task.Delay(-1);
        }

        private void SaveConfig()
        {
            File.WriteAllText("BotConfig.json", Newtonsoft.Json.JsonConvert.SerializeObject(_botConfig));
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task MotusGame(SocketMessage message)
        {
            if (message.Channel.Name.StartsWith("motus") == false || message.Content.Length <= 0)
                return;
            ulong channel_id = message.Channel.Id;
            if (_motusGames.ContainsKey(channel_id) == false)
            {
                if (_botConfig.games_configs.ContainsKey(channel_id))
                    _motusGames[channel_id] = new MotusGame(_botConfig.games_configs[channel_id].Copy(), channel_id);
                else
                    _motusGames[channel_id] = new MotusGame(new GameConfig(), channel_id);
            }
            _motusGames[channel_id].UpdateGameTime();
            _motusMessagesFormulator.SetLanguage(_motusGames[channel_id].GetLanguage());
            if (message.Content[0] == '!')
            {
                var word = message.Content.Trim('!', ' ').ToLower();
                if (_motusGames[channel_id].IsValidPlay(word))
                {
                    await message.Channel.SendMessageAsync(_motusMessagesFormulator.MessagePlayWord(_motusGames[channel_id].CommandPlayWord(message.Author.Username, word), _motusGames[channel_id].GetGameTurnClues(), _motusGames[channel_id].GetNeedle(), _motusGames[channel_id].GetGamePlayersPoints()));
                }
            }
            else if (message.Content == "/start")
                await message.Channel.SendMessageAsync(_motusMessagesFormulator.MessageGameStart(_motusGames[channel_id].CommandStartGame(), _motusGames[channel_id].GetConfig(), _motusGames[channel_id].GetGameTurnClues()));
            else if (message.Content == "/abort")
                await message.Channel.SendMessageAsync(_motusMessagesFormulator.MessageAbort(_motusGames[channel_id].CommandAbort(), _motusGames[channel_id].GetNeedle()));
            else if (message.Content.StartsWith("/top") && message.Content.Length > 4)
            {
                int n;
                int.TryParse(message.Content.Substring(4), out n);
                if (n >= 2 && n <= 10)
                    await message.Channel.SendMessageAsync(_motusMessagesFormulator.MessageTopScores(_motusGames[channel_id].GetTopScores(n)));
            }
            else if (message.Content == "/hardmode")
                await message.Channel.SendMessageAsync(_motusMessagesFormulator.MessageHardMode(_motusGames[channel_id].CommandHardMode()));
            else if (message.Content == "/usage" || message.Content == "/help")
                await message.Channel.SendMessageAsync(_motusMessagesFormulator.MessageUsage());
            else if (message.Content == "/nodiacritics")
                await message.Channel.SendMessageAsync(_motusMessagesFormulator.MessageRemoveDiacritics(_motusGames[channel_id].CommandRemoveDiacritics()));
            else if (message.Content == "/nocompounds")
                await message.Channel.SendMessageAsync(_motusMessagesFormulator.MessageRemoveCompounds(_motusGames[channel_id].CommandRemoveCompounds()));
            else if (message.Content == "/hints")
                await message.Channel.SendMessageAsync(_motusMessagesFormulator.MessageEnableHints(_motusGames[channel_id].CommandEnableHints()));
            else if (message.Content.StartsWith("/size") && message.Content.Length > 5)
            {
                ushort n;
                ushort.TryParse(message.Content.Substring(5), out n);
                if (n > 0)
                    await message.Channel.SendMessageAsync(_motusMessagesFormulator.MessageChangeSize(_motusGames[channel_id].CommandSetWordSize(n)));
            }
            else if (message.Content.StartsWith("/lang") && message.Content.Length > 5)
            {
                string lang = message.Content.Remove(0, 5).Trim().ToLower();
                if (_motusMessagesFormulator._language_folders.Contains(lang) && lang != _motusGames[channel_id].GetLanguage())
                    await message.Channel.SendMessageAsync(_motusMessagesFormulator.MessageSetLanguage(_motusGames[channel_id].CommandSetLanguage(lang), lang));
            }
            if (_botConfig.games_configs.ContainsKey(channel_id) == false || _motusGames[channel_id].GetConfig().Equals(_botConfig.games_configs[channel_id]) == false)
            {
                _botConfig.games_configs[channel_id] = _motusGames[channel_id].GetConfig().Copy();
                SaveConfig();
            }
        }
    }
}
