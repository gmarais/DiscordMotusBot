using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordMotusBot
{
    public class MotusMessagesFormulator
    {
        public List<string> _language_folders = new List<string>(new string[] { "en", "fr" });
        private string _language_folder = "en";
        private enum eMessageTypes
        {
            USAGE,
            ERR_NO_GAME,
            ERR_NO_WORDS,
            ERR_GAME_STILL_RUNNING,
            GAME_STARTED,
            WARN_DIACRITICS,
            WARN_COMPOUNDS,
            HERE_IS_A_HINT,
            FAIL_ATTEMPT,
            LAST_CHANCE,
            UNKNOWN_WORD,
            NEEDLE_FOUND,
            PLAYER_POINTS,
            SIZE_CHANGED,
            HARD_MODE,
            NO_DIACRITICS,
            NO_COMPOUNDS,
            HINTS_ENABLED,
            TOP_SCORES,
            RANK_PLAYER_SCORE,
            GAME_LOST,
            ABORT,
            LANGUAGE_CHANGED
        };
        private Dictionary<string, List<List<string>>> _messages = new Dictionary<string, List<List<string>>>();
        private Random _rnd = new Random();

        private bool LoadMessages()
        {
            var new_messages = new Dictionary<string, List<List<string>>>();
            foreach (var lang in _language_folders)
            {
                string messages_file_contents = null;
                if (File.Exists("Lang/" + lang + "/Messages.json"))
                    messages_file_contents = File.ReadAllText("Lang/" + lang + "/Messages.json");
                if (messages_file_contents != null)
                {
                    var tmp = JsonConvert.DeserializeObject<List<List<string>>>(messages_file_contents);
                    if (tmp.Count >= Enum.GetNames(typeof(eMessageTypes)).Length)
                        new_messages.Add(lang, tmp);
                    else
                        return false;
                }
                else
                    return false;
            }
            _messages = new_messages;
            return true;
        }

        public MotusMessagesFormulator()
        {
            if (LoadMessages() == false)
                Console.WriteLine("ERROR: Could not properly load default messages...");
        }

        private string GetMessageString(eMessageTypes type)
        {
            int index = (int)type;
            if (_messages[_language_folder].Count > index && _messages[_language_folder][index].Count > 0)
                return _messages[_language_folder][index][_rnd.Next(_messages[_language_folder][index].Count)];
            return "Translation missing (" + (int)type + ")";
        }

        public void SetLanguage(string language_folder)
        {
            if (_language_folders.Contains(language_folder))
                _language_folder = language_folder;
        }

        public string MessageSetLanguage(eMotusErrors err, string language_folder)
        {
            if (err == eMotusErrors.MISSING_WORDS)
                return GetMessageString(eMessageTypes.ERR_NO_WORDS);
            SetLanguage(language_folder);
            return GetMessageString(eMessageTypes.LANGUAGE_CHANGED);
        }

        private string CreateHintFromTurnClues(GameTurnClues clues)
        {
            string hint = "";
            bool underlined = false;
            for (int i = 0; i < clues.needle_length; i++)
            {
                if (clues.letters_placed.ContainsKey(i))
                {
                    if (underlined == false)
                    {
                        underlined = true;
                        hint += "`";
                    }
                    hint += clues.letters_placed[i];
                }
                else
                {
                    if (underlined == true)
                    {
                        underlined = false;
                        hint += "`";
                    }
                    if (clues.letters_missplaced.ContainsKey(i))
                        hint += clues.letters_missplaced[i];
                    else
                        hint += "•";
                }
            }
            if (underlined == true)
                hint += "`";
            return hint;
        }

        public string MessageGameStart(eMotusErrors err, GameConfig game_config, GameTurnClues clues)
        {
            if (err == eMotusErrors.GAME_STILL_RUNNING)
                return GetMessageString(eMessageTypes.ERR_GAME_STILL_RUNNING);
            else if (err == eMotusErrors.MISSING_WORDS)
                return GetMessageString(eMessageTypes.ERR_NO_WORDS);
            string response;
            response = GetMessageString(eMessageTypes.GAME_STARTED);
            response = String.Format(response, game_config.size);
            if (game_config.diacritics && File.Exists("Lang/" + _language_folder + "/diacritics_multiplier.txt"))
                response += GetMessageString(eMessageTypes.WARN_DIACRITICS);
            if (game_config.compounds && File.Exists("Lang/" + _language_folder + "/compounds_multiplier.txt"))
                response += GetMessageString(eMessageTypes.WARN_COMPOUNDS);
            if (game_config.hints)
                response += String.Format(GetMessageString(eMessageTypes.HERE_IS_A_HINT), CreateHintFromTurnClues(clues));
            return response;
        }

        public string MessagePlayWord(eMotusErrors err, GameTurnClues clues, string needle, Dictionary<string, int> players_points)
        {
            if (err == eMotusErrors.NO_ONGOING_GAME)
            {
                return GetMessageString(eMessageTypes.ERR_NO_GAME);
            }
            string response;
            if (err == eMotusErrors.GAME_OVER)
            {
                if (clues.needle_found)
                {
                    response = GetMessageString(eMessageTypes.NEEDLE_FOUND);
                    response = String.Format(response, players_points.Keys.ElementAt(0), players_points.Values.ElementAt(0));
                    for (int i = 1; i < players_points.Count; i++)
                    {
                        response += String.Format(GetMessageString(eMessageTypes.PLAYER_POINTS), players_points.Keys.ElementAt(i), players_points.Values.ElementAt(i));
                    }
                    response += "\n<https://" + _language_folder + ".wiktionary.org/wiki/" + needle + ">";
                    return response;
                }
                else
                {
                    response = GetMessageString(eMessageTypes.GAME_LOST);
                    response = String.Format(response, needle);
                    response += "\n<https://" + _language_folder + ".wiktionary.org/wiki/" + needle + ">";
                    return response;
                }
            }
            if (clues.tries_left > 1)
                response = String.Format(GetMessageString(eMessageTypes.FAIL_ATTEMPT), CreateHintFromTurnClues(clues), clues.tries_left);
            else
                response = String.Format(GetMessageString(eMessageTypes.LAST_CHANCE), CreateHintFromTurnClues(clues), clues.tries_left);
            if (err == eMotusErrors.WARNING_UNKNOWN_WORD)
                response += GetMessageString(eMessageTypes.UNKNOWN_WORD);
            return response;
        }

        public string MessageHardMode(eMotusErrors err)
        {
            if (err == eMotusErrors.MISSING_WORDS)
                return GetMessageString(eMessageTypes.ERR_NO_WORDS);
            return GetMessageString(eMessageTypes.HARD_MODE);
        }

        public string MessageRemoveDiacritics(eMotusErrors err)
        {
            return GetMessageString(eMessageTypes.NO_DIACRITICS);
        }

        public string MessageRemoveCompounds(eMotusErrors err)
        {
            return GetMessageString(eMessageTypes.NO_COMPOUNDS);
        }

        public string MessageEnableHints(eMotusErrors err)
        {
            return GetMessageString(eMessageTypes.HINTS_ENABLED);
        }

        public string MessageChangeSize(eMotusErrors err)
        {
            if (err == eMotusErrors.MISSING_WORDS)
                return GetMessageString(eMessageTypes.ERR_NO_WORDS);
            return GetMessageString(eMessageTypes.SIZE_CHANGED);
        }

        public string MessageAbort(eMotusErrors err, string needle)
        {
            string response = String.Format(GetMessageString(eMessageTypes.ABORT), needle);
            response += "\n<https://" + _language_folder + ".wiktionary.org/wiki/" + needle + ">";
            return response;
        }

        public string MessageTopScores(IEnumerable<KeyValuePair<string, int>> scores)
        {
            string response = String.Format(GetMessageString(eMessageTypes.TOP_SCORES), scores.Count());
            string rps = GetMessageString(eMessageTypes.RANK_PLAYER_SCORE);
            for (int i = 0; i < scores.Count(); i++)
            {
                response += String.Format(rps, i + 1, scores.ElementAt(i).Key, scores.ElementAt(i).Value);
            }
            return response;
        }

        public string MessageUsage()
        {
            return GetMessageString(eMessageTypes.USAGE);
        }
    }
}
