using System;
using System.Linq;
using Discord;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Games
{
    public abstract class MultiplayerGame : ChannelGame, IMultiplayerGame
    {
        public virtual Player Turn { get; protected set; }
        public virtual Player Winner { get; protected set; }
        public virtual string Message { get; protected set; }

        public virtual bool BotTurn => State == State.Active && (User(Turn)?.IsBot ?? false);
        public virtual bool AllBots => Enumerable.Range(0, UserId.Length).All(x => User(x)?.IsBot ?? false);

        public IUser User(Player player) => User((int)player);
        public IUser User(int i = 0)
        {
            if (i < 0 || i >= UserId.Length) return null;
            return client.GetUser(UserId[i]);
        }



        // AI flavor text

        public static readonly string[] StartTexts = {
            "I'll give it a go", "Let's do this", "Dare to defy the gamemaster?", "May the best win", "I was getting bored!",
            "Maybe you should play with a real person instead", "In need of friends to play with?"
        };
        public static readonly string[] GameTexts = {
            "🤔", "🔣", "🤖", CustomEmoji.Thinkxel, CustomEmoji.PacMan, "Hmm...", "Nice move.", "Take this!", "Huh.", "Aha!",
            "Come on now", "All according to plan", "I think I'm winning this one", "Beep boop", "Boop?", "Interesting...",
            "Recalculating...", "ERROR: YourSkills not found", "I wish to be a real bot", "That's all you got?",
            "Let's see what happens", "I don't even know what I'm doing", "This is a good time for you to quit", "Curious."
        };
        public static readonly string[] WinTexts = {
            "👍", CustomEmoji.PacMan, CustomEmoji.RapidBlobDance, "Rekt", "Better luck next time", "Beep!", ":)", "Nice",
            "Muahaha", "You weren't even trying"
        };
        public static readonly string[] NotWinTexts = {
            "Oof", "No u", "Foiled again!", "Boo...", "Ack", "Good job!", "gg", "You're good at this", "I let you win, of course"
        };



        // Methods

        public static TGame CreateNew<TGame>(ulong channelId, SocketUser[] players, IServiceProvider services)
            where TGame : MultiplayerGame
        {
            if (typeof(TGame).IsAbstract) throw new ArgumentException("Cannot instatiate abstract class");

            var game = (TGame)Activator.CreateInstance(typeof(TGame), true);
            game.Initialize(channelId, players, services);
            return game;
        }


        protected virtual void Initialize(ulong channelId, SocketUser[] players, IServiceProvider services)
        {
            SetServices(services);

            ChannelId = channelId;
            if (players != null) UserId = players.Select(x => x.Id).ToArray();
            LastPlayed = DateTime.Now;
            Turn = Player.First;
            Winner = Player.None;
            Message = "";
        }


        public abstract void BotInput();


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
                    return $"{User(0).Mention} You were invited to play {Name}.\nChoose an action below, " +
                           $"or type `{storage.GetPrefix(Guild)}cancel` if you don't want to play";
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
            if (turn > 0 && (int)turn < UserId.Length) return (Player)((int)turn - 1);
            return (Player)(UserId.Length - 1);
        }


        protected string StripPrefix(string value)
        {
            string prefix = storage.GetPrefix(Guild);
            return value.StartsWith(prefix) ? value.Substring(prefix.Length) : value;
        }


        protected string EmbedTitle()
        {
            return Winner == Player.None ? $"{Turn.ToStringColor()} Player's turn" :
                   Winner == Player.Tie ? "It's a tie!" :
                   UserId[0] != UserId[1] ? $"{Turn} is the winner!" :
                   UserId[0] == client.CurrentUser.Id ? "I win!" : "A winner is you!"; // These two are for laughs
        }
    }
}
