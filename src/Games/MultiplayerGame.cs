using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using PacManBot.Extensions;

namespace PacManBot.Games
{
    /// <summary>
    /// A specialized type of game that is channel-based and allows for inter-player mechanics as well as AI.
    /// Implements <see cref="IMultiplayerGame"/>.<para/>
    /// Instances of its subclasses must be created using <see cref="CreateNewAsync{TGame}(ulong, SocketUser[], IServiceProvider)"/>.
    /// </summary>
    public abstract class MultiplayerGame : ChannelGame, IMultiplayerGame
    {
        private DiscordUser[] internalUsers;


        /// <summary>The current <see cref="Player"/> whose turn it is.</summary>
        public virtual Player Turn { get; protected set; }

        /// <summary>This game's winner. <see cref="Player.None"/> if none.</summary>
        public virtual Player Winner { get; protected set; }

        /// <summary>The message displayed at the top of this game.</summary>
        public virtual string Message { get; protected set; }


        /// <summary>Whether the current turn belongs to a bot.</summary>
        public virtual async ValueTask<bool> IsBotTurnAsync()
        {
            return State == GameState.Active && ((await GetUserAsync(Turn))?.IsBot ?? false);
        }


        /// <summary>Retrieves the user at the specified index at the time of game creation. Null if unreachable or not found.</summary>
        public virtual async ValueTask<DiscordUser> GetUserAsync(int i = 0)
        {
            if (i < 0 || i >= UserId.Length) return null;
            return internalUsers[i] ?? (internalUsers[i] = await (await GetClientAsync()).GetUserAsync(UserId[i]));
        }

        


        /// <summary>Creates a new instance of <typeparamref name="TGame"/> with the specified channel and players.</summary>
        public static async Task<TGame> CreateNewAsync<TGame>(ulong channelId, DiscordUser[] players, IServiceProvider services)
            where TGame : MultiplayerGame
        {
            if (typeof(TGame).IsAbstract) throw new ArgumentException("Cannot instatiate abstract class");

            var game = typeof(TGame).CreateInstance<TGame>();
            await game.InitializeAsync(channelId, players, services);
            return game;
        }


        /// <summary>Does the job of a constructor during
        /// <see cref="CreateNewAsync{TGame}(ulong, SocketUser[], IServiceProvider)"/>.</summary>
        protected virtual Task InitializeAsync(ulong channelId, DiscordUser[] players, IServiceProvider services)
        {
            SetServices(services);

            ChannelId = channelId;
            if (players != null)
            {
                UserId = players.Select(x => x.Id).ToArray();
                internalUsers = new DiscordUser[UserId.Length];
            }
            LastPlayed = DateTime.Now;
            Turn = 0;
            Winner = Player.None;
            Message = "";

            return Task.CompletedTask;
        }


        /// <summary>Executes automatic AI input, assuming it is a bot's turn.</summary>
        public abstract Task BotInputAsync();


        /// <summary>Default string content of a multiplayer game message. Displays flavor text in AI matches.</summary>
        public override async ValueTask<string> GetContentAsync(bool showHelp = true)
        {
            if (State != GameState.Cancelled && UserId.Count(id => id == shardedClient.CurrentUser.Id) == 1)
            {
                var texts = new[] { Message };

                if (Message == "")
                {
                    texts = Content.gameStartTexts;
                }
                else if (Time > 1 && Winner == Player.None)
                {
                    texts = Content.gamePlayingTexts;
                }
                else if (Winner != Player.None)
                {
                    texts = Winner != Player.Tie && UserId[Winner] == shardedClient.CurrentUser.Id
                        ? Content.gameWinTexts
                        : Content.gameNotWinTexts;
                }

                return Message = Program.Random.Choose(texts);
            }

            if (State == GameState.Active)
            {
                if (UserId[0] == UserId[1] && !UserId.Contains(shardedClient.CurrentUser.Id))
                {
                    return "Feeling lonely, or just testing the bot?";
                }
                if (Time == 0 && showHelp && UserId.Length > 1 && UserId[0] != UserId[1])
                {
                    return $"{(await GetUserAsync(0)).Mention} You were invited to play {GameName}.\nChoose an action below, " +
                           $"or type `{storage.GetPrefix(await GetChannelAsync())}cancel` if you don't want to play";
                }
            }

            return "";
        }


        /// <summary>Returns the next player in order, looping back to the first if necessary.</summary>
        protected Player NextPlayer()
        {
            if (Turn == Player.Tie) return Player.Tie;
            else if (Turn >= 0 && Turn < UserId.Length - 1) return Turn + 1;
            else return 0;
        }


        /// <summary>Returns the previous player in order, loopingback to the last if necessary.</summary>
        protected Player PreviousPlayer()
        {
            if (Turn == Player.Tie) return Player.Tie;
            if (Turn > 0 && Turn < UserId.Length) return Turn - 1;
            return UserId.Length - 1;
        }


        /// <summary>Default title of a multiplayer game embed, displaying the current turn/winner.</summary>
        protected string ColorEmbedTitle()
        {
            return Winner == Player.None ? $"{Turn.ColorName} Player's turn" :
                   Winner == Player.Tie ? "It's a tie!" :
                   UserId[0] != UserId[1] ? $"{Turn.ColorName} is the winner!" :
                   UserId[0] == shardedClient.CurrentUser.Id ? "I win!" : "A winner is you!"; // These two are for laughs
        }
    }
}
