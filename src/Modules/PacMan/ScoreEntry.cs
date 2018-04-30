using System;
using System.Linq;
using System.Collections.Generic;
using Discord.WebSocket;

namespace PacManBot.Modules.PacMan
{
    public class ScoreEntry
    {
        public static ScoreEntryComparer Comparer = new ScoreEntryComparer();

        public GameInstance.State state;
        public int score;
        public int turns;
        public ulong userId;
        public string username;
        public DateTime date;
        public string channel;


        public ScoreEntry(GameInstance.State state, int score, int turns, ulong userId, string username, DateTime date, string channel)
        {
            this.state = state;
            this.score = score;
            this.turns = turns;
            this.userId = userId;
            this.username = username;
            this.date = date;
            this.channel = channel;
        }


        public string GetUsername(DiscordShardedClient client)
        {
            return client.GetUser(userId)?.NameandNum() ?? username ?? "Unknown";
        }


        public override string ToString()
        {
            return $"{state} {score} {turns} {userId} \"{username.Replace('"', '“')}\" \"{date.ToString("o")}\" \"{channel.Replace('"', '“')}\"";
        }


        public string ToStringSimpleScoreboard(DiscordShardedClient client, int position)
        {
            return $"{position}. ({state}) {score} points in {turns} turns by user {GetUsername(client).SanitizeMarkdown().SanitizeMentions()}";
        }


        public static bool TryParse(string value, out ScoreEntry entry)
        {
            var splice = new List<string>(value.Split(' ', 5)); // state, score, turns, userId, (rest)

            if (splice.Count == 5)
            {
                splice.AddRange(splice[4].Split('"').Where(x => !string.IsNullOrWhiteSpace(x)));  // username, date, guild
                splice.RemoveAt(4);

                if (splice.Count == 7
                    && Enum.TryParse(splice[0], out GameInstance.State state)
                    && int.TryParse(splice[1], out int score)
                    && int.TryParse(splice[2], out int turns)
                    && ulong.TryParse(splice[3], out ulong userId)
                    && DateTime.TryParse(splice[5], out DateTime date)
                ){
                    string username = splice[4].Replace('“', '"');
                    string channel = splice[6].Replace('“', '"');

                    entry = new ScoreEntry(state, score, turns, userId, username, date, channel);
                    return true;
                }
            }

            entry = null;
            return false;
        }
    }


    // Allows me to add new score entries to the scoreboard in already-sorted position!
    public class ScoreEntryComparer : IComparer<ScoreEntry>
    {
        public int Compare(ScoreEntry x, ScoreEntry y)
        {
            return y.score.CompareTo(x.score);
        }
    }
}
