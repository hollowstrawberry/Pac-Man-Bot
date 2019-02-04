using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using PacManBot.Games;
using PacManBot.Services;

namespace PacManBot.Commands
{
    /// <summary>
    /// The base for a module that controls a game.
    /// </summary>
    /// <typeparam name="TGame">The game type that this module controls.</typeparam>
    public abstract class BaseGameModule<TGame> : BaseModule
        where TGame : class, IBaseGame
    {
        /// <summary>All services used to create new games.</summary>
        public IServiceProvider Services { get; set; }

        /// <summary>Gives access to command information.</summary>
        public PmCommandService Commands { get; set; }

        /// <summary>Gives access to active games.</summary>
        public GameService Games { get; set; }

        /// <summary>The active game for this context.</summary>
        public TGame Game { get; private set; }


        protected override void BeforeExecute(CommandInfo command)
        {
            base.BeforeExecute(command);
            Game = GetCurrentGame();
        }

        /// <summary>Obtains the game for the current context from the game service. Used to set <see cref="Game"/>.</summary>
        protected virtual TGame GetCurrentGame()
        {
            return typeof(IUserGame).IsAssignableFrom(typeof(TGame))
                ? Games.GetForUser(Context.User.Id, typeof(TGame)) as TGame
                : Games.GetForChannel(Context.Channel.Id) as TGame;
        }




        /// <summary>Deletes the game for this context from the bot.</summary>
        public void EndGame(GameState state = GameState.Cancelled)
        {
            Game.State = state;
            if (Game != null) Games.Remove(Game);
        }

        /// <summary>Adds a new game for this context to the bot.</summary>
        public void StartNewGame(TGame game)
        {
            Games.Add(Game = game);
        }

        /// <summary>Saves the game to disk if the game for this context is storeable.</summary>
        public async Task SaveGameAsync()
        {
            if (Game != null && Game is IStoreableGame sgame) await Games.SaveAsync(sgame);
        }




        /// <summary>Sends the game for this context in chat.</summary>
        public async Task<IUserMessage> ReplyGameAsync(string overrideContent = null)
        {
            var msg = await ReplyAsync(overrideContent ?? Game.GetContent(), Game.GetEmbed());
            if (Game is IChannelGame cgame)
            {
                cgame.MessageId = msg.Id;
                cgame.ChannelId = Context.Channel.Id;
            }
            return msg;
        }


        /// <summary>Safely tries to remove the game's current message from chat.</summary>
        public async Task DeleteGameMessageAsync()
        {
            if (!(Game is IChannelGame cgame)) return;
            try
            {
                cgame.CancelRequests();
                var msg = await cgame.GetMessageAsync();
                if (msg != null) await msg.DeleteAsync(DefaultOptions);
            }
            catch (HttpException) { } // Something happened to the message, not important
        }


        /// <summary>Safely modifies the game's current message in the chat.</summary>
        public async Task<IUserMessage> UpdateGameMessageAsync()
        {
            if (!(Game is IChannelGame cgame)) return null;

            cgame.CancelRequests();
            var msg = await cgame.GetMessageAsync();
            try
            {
                if (msg != null) await msg.ModifyAsync(cgame.GetMessageUpdate(), cgame.GetRequestOptions());
                return msg;
            }
            catch (HttpException) { return null; }
            catch (OperationCanceledException) { return msg; }
        }


        /// <summary>Tries to update an existing game message, otherwise sends a new one.</summary>
        public async Task<IUserMessage> SendOrUpdateGameMessageAsync()
        {
            var msg = Game is IChannelGame cgame ? await cgame.GetMessageAsync() : null;
            if (msg != null) msg = await UpdateGameMessageAsync();
            if (msg == null) msg = await ReplyGameAsync(); // Didn't exist or failed to update
            return msg;
        }




        /// <summary>If a game already exists in this channel, sends a message and returns true.</summary>
        public async Task<bool> CheckGameAlreadyExistsAsync()
        {
            var existing = Games.GetForChannel(Context.Channel.Id);
            if (existing != null)
            {
                await ReplyAsync(existing.UserId.Contains(Context.User.Id)
                    ? $"You're already playing a game in this channel!\nUse `{Context.Prefix}cancel` if you want to cancel it."
                    : $"There is already a different game in this channel!\nWait until it's finished or try doing `{Context.Prefix}cancel`");
                return true;
            }
            return false;
        }
    }
}
