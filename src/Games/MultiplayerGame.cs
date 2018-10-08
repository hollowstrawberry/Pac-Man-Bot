using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using PacManBot.Utils;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Games
{
    /// <summary>
    /// A specialized type of game that is channel-based and allows for inter-player mechanics as well as AI.
    /// Implements <see cref="IMultiplayerGame"/>.<para/>
    /// Instances of its subclasses must be created using <see cref="CreateNew{TGame}(ulong, SocketUser[], IServiceProvider)"/>.
    /// </summary>
    public abstract class MultiplayerGame : ChannelGame, IMultiplayerGame
    {
        private IUser[] internalUsers;


        /// <summary>The current <see cref="Player"/> whose turn it is.</summary>
        public virtual Player Turn { get; protected set; }

        /// <summary>This game's winner. <see cref="Player.None"/> if none.</summary>
        public virtual Player Winner { get; protected set; }

        /// <summary>The message displayed at the top of this game.</summary>
        public virtual string Message { get; protected set; }


        /// <summary>Whether the current turn belongs to a bot.</summary>
        public virtual bool BotTurn => State == State.Active && (User(Turn)?.IsBot ?? false);

        /// <summary>Whether this game's players are all bots.</summary>
        public virtual bool AllBots => new Range(UserId.Length).All(x => User(x)?.IsBot ?? false);


        /// <summary>Retrieves the user at the specified index at the time of game creation. Null if unreachable or not found.</summary>
        public virtual IUser User(int i = 0)
        {
            if (i < 0 || i >= UserId.Length) return null;
            return internalUsers[i] ?? (internalUsers[i] = client.GetUser(UserId[i]));
        }

        


        /// <summary>Creates a new instance of <typeparamref name="TGame"/> with the specified channel and players.</summary>
        public static async Task<TGame> CreateNew<TGame>(ulong channelId, SocketUser[] players, IServiceProvider services)
            where TGame : MultiplayerGame
        {
            if (typeof(TGame).IsAbstract) throw new ArgumentException("Cannot instatiate abstract class");

            var game = (TGame)Activator.CreateInstance(typeof(TGame), true);
            await game.Initialize(channelId, players, services);
            return game;
        }


        /// <summary>Does the job of a constructor during
        /// <see cref="CreateNew{TGame}(ulong, SocketUser[], IServiceProvider)"/>.</summary>
        protected virtual Task Initialize(ulong channelId, SocketUser[] players, IServiceProvider services)
        {
            SetServices(services);

            ChannelId = channelId;
            if (players != null)
            {
                UserId = players.Select(x => x.Id).ToArray();
                internalUsers = new IUser[UserId.Length];
            }
            LastPlayed = DateTime.Now;
            Turn = 0;
            Winner = Player.None;
            Message = "";

            return Task.CompletedTask;
        }


        /// <summary>Executes automatic AI input, assuming it is a bot's turn.</summary>
        public abstract void BotInput();


        /// <summary>Default string content of a multiplayer game message. Displays flavor text in AI matches.</summary>
        public override string GetContent(bool showHelp = true)
        {
            if (State != State.Cancelled && UserId.Count(id => id == client.CurrentUser.Id) == 1)
            {
                var texts = new[] { Message };

                if (Message == "")
                {
                    texts = Content.gameStartTexts;
                }
                else if (Time > 1 && Winner == Player.None && (!AllBots || Time % 2 == 0))
                {
                    texts = Content.gamePlayingTexts;
                }
                else if (Winner != Player.None)
                {
                    texts = Winner != Player.Tie && UserId[Winner] == client.CurrentUser.Id
                        ? Content.gameWinTexts
                        : Content.gameNotWinTexts;
                }

                return Message = Bot.Random.Choose(texts);
            }

            if (State == State.Active)
            {
                if (UserId[0] == UserId[1] && !UserId.Contains(client.CurrentUser.Id))
                {
                    return "Feeling lonely, or just testing the bot?";
                }
                if (Time == 0 && showHelp && UserId.Length > 1 && UserId[0] != UserId[1])
                {
                    return $"{User(0).Mention} You were invited to play {GameName}.\nChoose an action below, " +
                           $"or type `{storage.GetPrefix(Channel)}cancel` if you don't want to play";
                }
            }

            return "";
        }


        /// <summary>Returns the next player in order, looping back to the first if necessary.</summary>
        protected Player NextPlayer(Player? turn = null)
        {
            if (turn == null) turn = Turn;

            if (turn == Player.Tie) return Player.Tie;
            else if (turn >= 0 && turn < UserId.Length - 1) return turn.Value + 1;
            else return 0;
        }


        /// <summary>Returns the previous player in order, loopingback to the last if necessary.</summary>
        protected Player PreviousPlayer(Player? turn = null)
        {
            if (turn == null) turn = Turn;

            if (turn == Player.Tie) return Player.Tie;
            if (turn > 0 && turn < UserId.Length) return turn.Value - 1;
            return UserId.Length - 1;
        }


        /// <summary>Used to remove the guild prefix from game input, as it is to be ignored.</summary>
        protected string StripPrefix(string value)
        {
            string prefix = storage.GetPrefix(Channel);
            return value.StartsWith(prefix) ? value.Substring(prefix.Length) : value;
        }


        /// <summary>Default title of a multiplayer game embed, displaying the current turn/winner.</summary>
        protected string ColorEmbedTitle()
        {
            return Winner == Player.None ? $"{Turn.ColorName} Player's turn" :
                   Winner == Player.Tie ? "It's a tie!" :
                   UserId[0] != UserId[1] ? $"{Turn.ColorName} is the winner!" :
                   UserId[0] == client.CurrentUser.Id ? "I win!" : "A winner is you!"; // These two are for laughs
        }
    }
}
