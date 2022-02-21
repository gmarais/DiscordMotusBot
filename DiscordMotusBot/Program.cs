using System;
using Discord;
using Discord.WebSocket;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Security.Cryptography;

namespace DiscordMotusBot
{
    internal class Program
    {
        private Dictionary<ulong, GameConfig> _gamesConfigs = null;
        private Dictionary<ulong, MotusGame> _motusGames = new Dictionary<ulong, MotusGame>();
        private MotusMessagesFormulator _motusMessagesFormulator = new MotusMessagesFormulator();
        private DiscordSocketClient _client;

        public Program()
        {
            WebServer.Start();
        }
      
        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient();
            _client.Log += Log;
            _client.MessageReceived += MotusGame;

            if (EncryptedSaves.SaveExists("GAMES_CONFIGS"))
                _gamesConfigs = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<ulong, GameConfig>>(EncryptedSaves.Load("GAMES_CONFIGS"));
            else
                _gamesConfigs = new Dictionary<ulong, GameConfig>();
            await _client.LoginAsync(TokenType.Bot, System.Environment.GetEnvironmentVariable("DKEY"));
            await _client.StartAsync();
            await Task.Delay(-1);
        }

        private void SaveConfig()
        {
            EncryptedSaves.Save("GAMES_CONFIGS", Newtonsoft.Json.JsonConvert.SerializeObject(_gamesConfigs));
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
                if (_gamesConfigs.ContainsKey(channel_id))
                    _motusGames[channel_id] = new MotusGame(_gamesConfigs[channel_id].Copy(), channel_id);
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
            if (_gamesConfigs.ContainsKey(channel_id) == false || _motusGames[channel_id].GetConfig().Equals(_gamesConfigs[channel_id]) == false)
            {
                _gamesConfigs[channel_id] = _motusGames[channel_id].GetConfig().Copy();
                SaveConfig();
            }
        }
    }
}
