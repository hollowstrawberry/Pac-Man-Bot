using System;
using System.Linq;
using System.Collections.Generic;
using Discord.WebSocket;
using PacManBot.Games;
using PacManBot.Extensions;

namespace PacManBot.Utils
{
    public class ScoreEntry : IComparable<ScoreEntry>
    {
        public State state;
        public int score;
        public int turns;
        public ulong userId;
        public string username;
        public DateTime date;
        public string channel;


        public ScoreEntry(int score, ulong userId, State state, int turns, string username, string channel, DateTime date)
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
            return client?.GetUser(userId)?.NameandNum() ?? username ?? "Unknown";
        }


        public override string ToString() => ToString(null);

        public string ToString(DiscordShardedClient client)
        {
            return $"({state}) {score} points in {turns} turns by user " +
                   $"{GetUsername(client).SanitizeMarkdown().SanitizeMentions()}";
        }


        public int CompareTo(ScoreEntry other)
        {
            return other.score.CompareTo(this.score);
        }
    }
}
