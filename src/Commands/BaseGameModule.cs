using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Services;

namespace PacManBot.Commands
{
    /// <summary>
    /// The base for a module that controls a game.
    /// </summary>
    /// <typeparam name="TGame">The game type that this module controls.</typeparam>
    public abstract class BaseGameModule<TGame> : BasePmBotModule
        where TGame : class, IBaseGame
    {
        /// <summary>Obtains the game for the current context from the game service.</summary>
        protected virtual TGame Game(CommandContext ctx)
        {
            return typeof(IUserGame).IsAssignableFrom(typeof(TGame))
                ? Games.GetForUser(ctx.User.Id, typeof(TGame)) as TGame
                : Games.GetForChannel(ctx.Channel.Id) as TGame;
        }


        /// <summary>Deletes the game for this context from the bot.</summary>
        public void EndGame(CommandContext ctx, GameState state = GameState.Cancelled)
        {
            Game(ctx).State = state;
            Games.Remove(Game(ctx));
        }


        /// <summary>Adds a new game for this context to the bot.</summary>
        public TGame StartNewGame(TGame game)
        {
            Games.Add(game);
            return game;
        }


        /// <summary>Saves the game to disk if the game for this context is storeable.</summary>
        public async Task SaveGameAsync(CommandContext ctx)
        {
            if (Game(ctx) != null && Game(ctx) is IStoreableGame sgame) await Games.SaveAsync(sgame);
        }




        /// <summary>Sends the game for this context in chat.</summary>
        public async Task<DiscordMessage> RespondGameAsync(CommandContext ctx, string overrideContent = null)
        {
            var msg = await ctx.RespondAsync(overrideContent ?? await Game(ctx).GetContentAsync(), await Game(ctx).GetEmbedAsync());
            if (Game(ctx) is IChannelGame cgame)
            {
                cgame.MessageId = msg.Id;
                cgame.ChannelId = ctx.Channel.Id;
            }
            return msg;
        }


        /// <summary>Safely tries to remove the game's current message from chat.</summary>
        public async Task DeleteGameMessageAsync(CommandContext ctx)
        {
            if (!(Game(ctx) is IChannelGame cgame)) return;
            try
            {
                var msg = await cgame.GetMessageAsync();
                if (msg != null) await msg.DeleteAsync();
            }
            catch (NotFoundException) { }
        }


        /// <summary>Safely modifies the game's current message in the chat.</summary>
        public async Task<DiscordMessage> UpdateGameMessageAsync(CommandContext ctx)
        {
            if (!(Game(ctx) is IChannelGame cgame)) return null;

            var msg = await cgame.GetMessageAsync();
            try
            {
                if (msg != null) await msg.ModifyAsync(await cgame.GetContentAsync(), (await cgame.GetEmbedAsync()).Build());
                return msg;
            }
            catch (NotFoundException) { return null; }
        }


        /// <summary>Tries to update an existing game message, otherwise sends a new one.</summary>
        public async Task<DiscordMessage> SendOrUpdateGameMessageAsync(CommandContext ctx)
        {
            var msg = Game(ctx) is IChannelGame cgame ? await cgame.GetMessageAsync() : null;
            if (msg != null) msg = await UpdateGameMessageAsync(ctx);
            if (msg == null) msg = await RespondGameAsync(ctx); // Didn't exist or failed to update
            return msg;
        }


        /// <summary>If a game already exists in this channel, sends a message and returns true.</summary>
        public async Task<bool> CheckGameAlreadyExistsAsync(CommandContext ctx)
        {
            var existing = Games.GetForChannel(ctx.Channel.Id);
            if (existing != null)
            {
                string prefix = Storage.GetPrefix(ctx.Channel);
                await ctx.RespondAsync(existing.UserId.Contains(ctx.User.Id)
                    ? $"You're already playing a game in this channel!\nUse `{prefix}cancel` if you want to cancel it."
                    : $"There is already a different game in this channel!\nWait until it's finished or try doing `{prefix}cancel`");
                return true;
            }
            return false;
        }
    }
}
