using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.WebSocket;
using PacManBot.Services;
using static PacManBot.Games.GameUtils;

namespace PacManBot.Games
{
    public abstract class MultiplayerGame : ChannelGame, IMultiplayerGame
    {
        public virtual Player Turn { get; protected set; }
        public virtual Player Winner { get; protected set; }
        public virtual string Message { get; protected set; }

        public virtual bool AITurn => State == State.Active && (User(Turn)?.IsBot ?? false);
        public virtual bool AllBots => Enumerable.Range(0, UserId.Length).All(x => User(x)?.IsBot ?? false);

        public IUser User(Player player) => User((int)player);
        public IUser User(int i = 0)
        {
            if (i < 0 || i >= UserId.Length) return null;
            return client.GetUser(UserId[i]);
        }



        protected MultiplayerGame() : base() { }

        public virtual void Create(ulong channelId, ulong[] userId, DiscordShardedClient client, LoggingService logger, StorageService storage)
        {
            this.client = client;
            this.logger = logger;
            this.storage = storage;

            LastPlayed = DateTime.Now;
            ChannelId = channelId;
            if (userId != null) UserId = userId;
            Turn = Player.First;
            Winner = Player.None;
            Message = "";
        }

        public static TGame New<TGame>(ulong channelId, ulong[] userId, DiscordShardedClient client, LoggingService logger, StorageService storage) where TGame : MultiplayerGame
        {
            TGame game = Activator.CreateInstance<TGame>();
            game.Create(channelId, userId, client, logger, storage);
            return game;
        }


        public abstract void DoTurnAI();


        public override string GetContent(bool showHelp = true)
        {
            if (State != State.Cancelled && UserId.Count(id => id == client.CurrentUser.Id) == 1)
            {
                if (Message == "") Message = Bot.Random.Choose(StartTexts);
                else if (Time > 1 && Winner == Player.None && (!AllBots || Time % 2 == 0)) Message = Bot.Random.Choose(GameTexts);
                else if (Winner != Player.None)
                {
                    if (Winner != Player.Tie && UserId[(int)Winner] == client.CurrentUser.Id) Message = Bot.Random.Choose(WinTexts);
                    else Message = Bot.Random.Choose(NotWinTexts);
                }

                return Message;
            }

            if (State == State.Active)
            {
                if (UserId[0] == UserId[1] && !UserId.Contains(client.CurrentUser.Id))
                {
                    return "Feeling lonely, or just testing the bot?";
                }
                if (Time == 0 && showHelp && UserId.Length > 1 && UserId[0] != UserId[1])
                {
                    return $"{User(0).Mention} You were invited to play {Name}.\nChoose an action below, or type `{storage.GetPrefix(Guild)}cancel` if you don't want to play";
                }
            }

            return "";
        }



        protected Player NextPlayer(Player? turn = null)
        {
            if (turn == null) turn = Turn;

            if (turn == Player.Tie) return Player.Tie;
            else if (turn >= 0 && (int)turn < UserId.Length - 1) return (Player)((int)turn + 1);
            else return 0;
        }

        protected Player PreviousPlayer(Player? turn = null)
        {
            if (turn == null) turn = Turn;

            if (turn == Player.Tie) return Player.Tie;
            else if (turn > 0 && (int)turn < UserId.Length) return (Player)((int)turn - 1);
            else return (Player)(UserId.Length - 1);
        }


        protected string StripPrefix(string value)
        {
            string prefix = storage.GetPrefix(Guild);
            if (value.StartsWith(prefix)) return value.Substring(prefix.Length);
            else return value;
        }


        protected string EmbedTitle()
        {
            return (Winner == Player.None) ? $"{Turn.ToStringColor()} Player's turn" :
                Winner == Player.Tie ? "It's a tie!" :
                UserId[0] != UserId[1] ? $"{Turn} is the winner!" :
                UserId[0] == client.CurrentUser.Id ? "I win!" : "A winner is you!"; // These two are for laughs
        }
    }
}
