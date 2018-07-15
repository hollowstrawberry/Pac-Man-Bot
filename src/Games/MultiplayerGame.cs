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
        /// <summary>The current <see cref="Player"/> whose turn it is.</summary>
        public virtual Player Turn { get; protected set; }

        /// <summary>This game's winner. <see cref="Player.None"/> if none.</summary>
        public virtual Player Winner { get; protected set; }

        /// <summary>The message displayed at the top of this game.</summary>
        public virtual string Message { get; protected set; }


        /// <summary>Whether the current turn belongs to a bot.</summary>
        public virtual bool BotTurn => State == State.Active && (User(Turn)?.IsBot ?? false);

        /// <summary>Whether this game's players are all bots.</summary>
        public virtual bool AllBots => Enumerable.Range(0, UserId.Length).All(x => User(x)?.IsBot ?? false);


        /// <summary>Retrieves the user corresponding to a <see cref="Player"/>. Null if unreachable or not found.</summary>
        public IUser User(Player player) => User((int)player);

        /// <summary>Retrieves the user at the specified index. Null if unreachable or not found.</summary>
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

        /// <summary>Creates a new instance of <typeparamref name="TGame"/> with the specified channel and players.</summary>
        public static TGame CreateNew<TGame>(ulong channelId, SocketUser[] players, IServiceProvider services)
            where TGame : MultiplayerGame
        {
            if (typeof(TGame).IsAbstract) throw new ArgumentException("Cannot instatiate abstract class");

            var game = (TGame)Activator.CreateInstance(typeof(TGame), true);
            game.Initialize(channelId, players, services);
            return game;
        }


        /// <summary>Performs the work of a constructor after this game instance is created using
        /// <see cref="CreateNew{TGame}(ulong, SocketUser[], IServiceProvider)"/>.</summary>
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


        /// <summary>Executes automatic AI input, assuming it is a bot's turn.</summary>
        public abstract void BotInput();


        /// <summary>Default string content of a multiplayer game message. Displays flavor text in AI matches.</summary>
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


        /// <summary>Returns the next player in order, looping back to the first if necessary.</summary>
        protected Player NextPlayer(Player? turn = null)
        {
            if (turn == null) turn = Turn;

            if (turn == Player.Tie) return Player.Tie;
            else if (turn >= 0 && (int)turn < UserId.Length - 1) return (Player)((int)turn + 1);
            else return 0;
        }


        /// <summary>Returns the previous player in order, loopingback to the last if necessary.</summary>
        protected Player PreviousPlayer(Player? turn = null)
        {
            if (turn == null) turn = Turn;

            if (turn == Player.Tie) return Player.Tie;
            if (turn > 0 && (int)turn < UserId.Length) return (Player)((int)turn - 1);
            return (Player)(UserId.Length - 1);
        }


        /// <summary>Used to remove the guild prefix from game input, as it is to be ignored.</summary>
        protected string StripPrefix(string value)
        {
            string prefix = storage.GetPrefix(Guild);
            return value.StartsWith(prefix) ? value.Substring(prefix.Length) : value;
        }


        /// <summary>Default title of a multiplayer game embed, displaying the current turn/winner.</summary>
        protected string EmbedTitle()
        {
            return Winner == Player.None ? $"{Turn.ToStringColor()} Player's turn" :
                   Winner == Player.Tie ? "It's a tie!" :
                   UserId[0] != UserId[1] ? $"{Turn} is the winner!" :
                   UserId[0] == client.CurrentUser.Id ? "I win!" : "A winner is you!"; // These two are for laughs
        }
    }
}
