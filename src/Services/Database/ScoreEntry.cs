using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Discord.WebSocket;
using PacManBot.Extensions;
using PacManBot.Games;

namespace PacManBot.Services.Database
{
    /// <summary>
    /// An entry in the <see cref="Games.Concrete.PacManGame"/> leaderboard.
    /// </summary>
    [Table("PacManScores")]
    public class ScoreEntry : IComparable<ScoreEntry>
    {
        public int Score { get; private set; }
        public ulong UserId { get; private set; }
        public GameState State { get; private set; }
        public int Turns { get; private set; }
        public string Username { get; private set; }
        public string Channel { get; private set; }
        [Key] public DateTime Date { get; private set; } // EF *requires* a key, because it hates me.

        public ScoreEntry(int score, ulong userId, GameState state, int turns, string username, string channel, DateTime date)
        {
            Score = score;
            UserId = userId;
            State = state;
            Turns = turns;
            Username = username;
            Channel = channel;
            Date = date;
        }


        public string GetUsername(BaseSocketClient client)
        {
            return client?.GetUser(UserId)?.NameandDisc() ?? Username ?? "Unknown";
        }


        public override string ToString() => ToString(null);

        public string ToString(BaseSocketClient client)
        {
            return $"({State}) {Score} points in {Turns} turns by user " +
                   $"{GetUsername(client).SanitizeMarkdown().SanitizeMentions()}";
        }


        public int CompareTo(ScoreEntry other)
        {
            return other.Score.CompareTo(this.Score);
        }
    }
}
