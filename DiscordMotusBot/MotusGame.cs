using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;

namespace DiscordMotusBot
{
    public struct GameTurnClues
    {
        public bool needle_found;
        public int needle_length;
        public int tries_left;
        public Dictionary<int, char> letters_placed;
        public Dictionary<int, char> letters_missplaced;
    }

    public class GameConfig
    {
        public string lang = "en";
        public bool diacritics = false;
        public bool compounds = false;
        public bool hints = true;
        public ushort size = 7;

        public GameConfig Copy()
        {
            GameConfig copy = new GameConfig();
            copy.lang = this.lang;
            copy.diacritics = this.diacritics;
            copy.compounds = this.compounds;
            copy.hints = this.hints;
            copy.size = this.size;
            return copy;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is GameConfig))
                return false;
            var other = obj as GameConfig;
            if (this.lang != other.lang || this.diacritics != other.diacritics || this.compounds != other.compounds || this.hints != other.hints || this.size != other.size)
                return false;
            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 13;
                hash += (hash * 7) + (lang == null ? 0 : lang.GetHashCode());
                hash += (hash * 7) + diacritics.GetHashCode();
                hash += (hash * 7) + compounds.GetHashCode();
                hash += (hash * 7) + hints.GetHashCode();
                hash += (hash * 7) + size.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(GameConfig x, GameConfig y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(GameConfig x, GameConfig y)
        {
            return !(x == y);
        }
    }

    public enum eMotusErrors
    {
        NONE,
        NO_ONGOING_GAME,
        GAME_STILL_RUNNING,
        INVALID_ENTRY,
        GAME_OVER,
        MISSING_WORDS,
        WARNING_UNKNOWN_WORD,
    }

    public class MotusGame
    {
        private GameConfig _config;
        private float _diacritics_multiplier;
        private float _compounds_multiplier;
        private ulong _channel_id;
        private Dictionary<string, int> _playerScores = new Dictionary<string, int>();
        private List<string> _stack;
        private int _needle_index;
        private string _needle;
        private int _max_tries = 6;
        private int _tries_count = 0;
        private Dictionary<int, char> _letters_to_find = new Dictionary<int, char>();
        private Dictionary<int, string> _letters_places_tried = new Dictionary<int, string>();
        private Dictionary<string, int> _game_player_points = new Dictionary<string, int>();
        private GameTurnClues _game_turn_clues;
        private Random _rnd = new Random();
        private bool _gameIsGoing;
        private DateTime _lastPlayTime;

        private void RemoveCompounds()
        {
            for (var w = 0; w < _stack.Count; w++)
            {
                while (_stack[w].Contains<char>('-'))
                {
                    _stack.RemoveAt(w);
                }
            }
        }

        private bool LoadDictionary(string language_folder, int words_size)
        {
            if (File.Exists("Lang/" + language_folder + "/Dictionaries/" + words_size + "L.txt"))
            {
                var words = File.ReadAllText("Lang/" + language_folder + "/Dictionaries/" + words_size + "L.txt", Encoding.Unicode);
                _stack = words.Split(' ').ToList();
                if (_config.compounds == false)
                    RemoveCompounds();
                return true;
            }
            return false;
        }

        private void LoadMultipliers(string language_folder)
        {
            if (File.Exists("Lang/" + language_folder + "/diacritics_multiplier.txt"))
                float.TryParse(File.ReadAllText("Lang/" + language_folder + "/diacritics_multiplier.txt"), out _diacritics_multiplier);
            else
                _diacritics_multiplier = 1f;
            if (File.Exists("Lang/" + language_folder + "/compounds_multiplier.txt"))
                float.TryParse(File.ReadAllText("Lang/" + language_folder + "/compounds_multiplier.txt"), out _compounds_multiplier);
            else
                _compounds_multiplier = 1f;
        }

        public MotusGame(GameConfig config, ulong channel_id)
        {
            _config = config;
            _channel_id = channel_id;
            LoadDictionary(_config.lang, _config.size);
            LoadMultipliers(_config.lang);
            if (EncryptedSaves.SaveExists(channel_id.ToString()))
            {
                var playerScoresContent = EncryptedSaves.Load(_channel_id.ToString());
                var playerScoresArray = playerScoresContent.Split('\n');
                for (var p = 0; p < playerScoresArray.Length; p++)
                {
                    var kv = playerScoresArray[p].Split('|');
                    _playerScores.Add(kv[0], int.Parse(kv[1]));
                }
            }
        }

        public void SaveScores()
        {
            var item = _playerScores.ElementAt(0);
            string scoresText = item.Key + "|" + item.Value;
            for (var i = 1; i < _playerScores.Count; i++)
            {
                item = _playerScores.ElementAt(i);
                scoresText += "\n" + item.Key + "|" + item.Value;
            }
            EncryptedSaves.Save(_channel_id.ToString(), scoresText);
        }

        public void UpdateGameTime()
        {
            if (_gameIsGoing)
            {
                var now = DateTime.Now;
                var diff = now - _lastPlayTime;
                if (diff.TotalDays > 1)
                    _gameIsGoing = false;
            }
        }

        private string RemoveDiacriticsFromWord(string word)
        {
            string text = word;
            if (string.IsNullOrWhiteSpace(text) == false)
            {
                text = text.Normalize(NormalizationForm.FormD);
                var chars = text.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
                return new string(chars).Normalize(NormalizationForm.FormC);
            }
            return text;
        }

        public IEnumerable<KeyValuePair<string, int>> GetTopScores(int max)
        {
            return (from ele in _playerScores
                    orderby ele.Value descending
                    select ele).Take(max);
        }

        public string GetLanguage()
        {
            return _config.lang;
        }

        public bool GameIsGoing()
        {
            return _gameIsGoing;
        }

        public GameConfig GetConfig()
        {
            return _config;
        }

        public string GetNeedle()
        {
            if (_gameIsGoing)
                return null;
            return _stack[_needle_index];
        }

        public Dictionary<string, int> GetGamePlayersPoints()
        {
            return _game_player_points;
        }

        public GameTurnClues GetGameTurnClues()
        {
            return _game_turn_clues;
        }

        public bool WordExists(string word)
        {
            if (_config.diacritics)
                return _stack.Contains(word);
            foreach (string w in _stack)
            {
                if (word == RemoveDiacriticsFromWord(w))
                    return true;
            }
            return false;
        }

        public bool IsValidPlay(string word)
        {
            if (_gameIsGoing && word.Length == _needle.Length && word.Contains<char>(' ') == false)
                return true;
            else
                return false;
        }

        public eMotusErrors CommandSetLanguage(string language_folder)
        {
            if (LoadDictionary(language_folder, _config.size))
            {
                LoadMultipliers(language_folder);
                _config.lang = language_folder;
                return eMotusErrors.NONE;
            }
            return eMotusErrors.MISSING_WORDS;
        }

        public eMotusErrors CommandSetWordSize(ushort size)
        {
            if (LoadDictionary(_config.lang, size))
            {
                _config.size = size;
                return eMotusErrors.NONE;
            }
            return eMotusErrors.MISSING_WORDS;
        }

        public eMotusErrors CommandHardMode()
        {
            _config.diacritics = true;
            _config.compounds = true;
            _config.hints = false;
            if (LoadDictionary(_config.lang, _config.size))
            {
                return eMotusErrors.NONE;
            }
            return eMotusErrors.MISSING_WORDS;
        }

        public eMotusErrors CommandEnableHints()
        {
            _config.hints = true;
            return eMotusErrors.NONE;
        }

        public eMotusErrors CommandRemoveDiacritics()
        {
            _config.diacritics = false;
            return eMotusErrors.NONE;
        }

        public eMotusErrors CommandRemoveCompounds()
        {
            RemoveCompounds();
            _config.compounds = false;
            return eMotusErrors.NONE;
        }

        private void SetStartingGameTurnClues()
        {
            _game_turn_clues = new GameTurnClues();
            if (_config.hints == false)
                return;
            _game_turn_clues.needle_length = _needle.Length;
            _game_turn_clues.letters_placed = new Dictionary<int, char>();
            _game_turn_clues.letters_missplaced = new Dictionary<int, char>();
            int number_of_revealed_letters = (_needle.Length - 3) / 2;
            while (number_of_revealed_letters > 0)
            {
                int letter_index = _rnd.Next(_letters_to_find.Count);
                var letter = _letters_to_find.ElementAt(letter_index);
                _game_turn_clues.letters_placed.Add(letter.Key, letter.Value);
                _letters_to_find.Remove(letter.Key);
                number_of_revealed_letters--;
            }
        }

        public eMotusErrors CommandStartGame()
        {
            if (_gameIsGoing)
                return eMotusErrors.GAME_STILL_RUNNING;
            if (_stack.Count <= 0)
                return eMotusErrors.MISSING_WORDS;
            _needle_index = _rnd.Next(_stack.Count);
            _needle = _stack[_needle_index];
            if (_config.diacritics == false)
                _needle = RemoveDiacriticsFromWord(_needle);
            _letters_places_tried.Clear();
            _letters_to_find.Clear();
            for (int c = 0; c < _needle.Length; c++)
            {
                _letters_to_find.Add(c, _needle[c]);
                _letters_places_tried.Add(c, "");
            }
            _tries_count = 0;
            _game_player_points.Clear();
            SetStartingGameTurnClues();
            _gameIsGoing = true;
            _lastPlayTime = DateTime.Now;
            return eMotusErrors.NONE;
        }

        public eMotusErrors CommandAbort()
        {
            if (!_gameIsGoing)
                return eMotusErrors.NO_ONGOING_GAME;
            _gameIsGoing = false;
            return eMotusErrors.NONE;
        }

        private void GrantPlayerPoints(string playerName, int points)
        {
            if (_game_player_points.ContainsKey(playerName))
                _game_player_points[playerName] += points;
            else
                _game_player_points[playerName] = points;
        }

        private void ReorderDictionary(string playerName)
        {
            Dictionary<string, int> new_dic = new Dictionary<string, int>();
            new_dic[playerName] = _game_player_points[playerName];
            foreach (var p in _game_player_points.OrderByDescending(key => key.Value))
            {
                if (p.Key != playerName)
                    new_dic[p.Key] = _game_player_points[p.Key];
            }
            _game_player_points = new_dic;
        }

        private eMotusErrors ProcessNeedleFound(string playerName)
        {
            var tmp = (_game_player_points.ContainsKey(playerName)) ? _game_player_points[playerName] : 0;
            GrantPlayerPoints(playerName, 45 * _letters_to_find.Count * (_game_turn_clues.tries_left + 1));
            ReorderDictionary(playerName);
            float multiplier = 1f;
            if (_config.diacritics && _diacritics_multiplier > 1f)
                multiplier += _diacritics_multiplier - 1f;
            if (_config.compounds && _compounds_multiplier > 1f)
                multiplier += _compounds_multiplier - 1f;
            foreach (var p in _game_player_points.Keys.ToList())
            {
                var temp = (int)((float)_game_player_points[p] * multiplier);
                _game_player_points[p] = temp / 10;
                if (temp - (_game_player_points[p] * 10) >= 5)
                    _game_player_points[p] += 1;
                if (_playerScores.ContainsKey(p))
                    _playerScores[p] += _game_player_points[p];
                else
                    _playerScores[p] = _game_player_points[p];
            }
            _gameIsGoing = false;
            SaveScores();
            _game_turn_clues.needle_found = true;
            return eMotusErrors.GAME_OVER;
        }

        private void PlayerPointsForLetterFound(string playerName, string word, bool letter_placed)
        {
            int points;
            if (WordExists(word) == false)
            {
                points = 5;
            }
            else
            {
                if (letter_placed)
                    points = 30 * (_game_turn_clues.tries_left + 1);
                else
                    points = 10 * (_game_turn_clues.tries_left + 1);
            }
            GrantPlayerPoints(playerName, points);
        }

        private int MissplacedCharCount(string word, char c)
        {
            int count = 0;
            for (int i = 0; i < _needle.Length; i++)
            {
                if (_needle[i] == c && word[i] != c)
                    count++;
            }
            return count;
        }

        private eMotusErrors ProcessWordTry(string playerName, string word)
        {
            var tmp = (_game_player_points.ContainsKey(playerName)) ? _game_player_points[playerName] : 0;
            _game_turn_clues.needle_length = _needle.Length;
            _game_turn_clues.letters_placed = new Dictionary<int, char>();
            _game_turn_clues.letters_missplaced = new Dictionary<int, char>();
            for (int l = 0; l < _needle.Length; l++)
            {
                if (_needle[l] == word[l])
                {
                    if (_letters_to_find.Keys.Contains(l) == true)
                    {
                        _letters_to_find.Remove(l);
                        PlayerPointsForLetterFound(playerName, word, true);
                    }
                    _game_turn_clues.letters_placed.Add(l, _needle[l]);
                }
                else
                {
                    if (_needle.Contains(word[l]) && MissplacedCharCount(word, word[l]) > 0)
                    {
                        if (_letters_to_find.Values.Contains(word[l]) && _letters_places_tried[l].Contains(word[l]) == false)
                        {
                            _letters_places_tried[l] += word[l];
                            PlayerPointsForLetterFound(playerName, word, false);
                        }
                        _game_turn_clues.letters_missplaced.Add(l, word[l]);
                    }
                }
            }
            if (WordExists(word) == false)
                return eMotusErrors.WARNING_UNKNOWN_WORD;
            return eMotusErrors.NONE;
        }

        public eMotusErrors CommandPlayWord(string playerName, string word)
        {
            _game_turn_clues = new GameTurnClues();
            _lastPlayTime = DateTime.Now;
            if (!IsValidPlay(word))
                return eMotusErrors.NO_ONGOING_GAME;
            if (_config.diacritics == false)
                word = RemoveDiacriticsFromWord(word);
            _tries_count++;
            _game_turn_clues.tries_left = _max_tries - _tries_count;
            if (_needle == word)
            {
                return ProcessNeedleFound(playerName);
            }
            if (_tries_count < _max_tries)
            {
                return ProcessWordTry(playerName, word);
            }
            _gameIsGoing = false;
            return eMotusErrors.GAME_OVER;
        }
    }
}
