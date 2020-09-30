using System;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using PacManBot.Games;

namespace PacManBot.Commands
{
    /// <summary>A module equipped to run multiplayer games.</summary>
    public abstract class MultiplayerGameModule<TGame> : BaseGameModule<TGame>
        where TGame : MultiplayerGame
    {
        protected override TGame Game(CommandContext ctx)
        {
            return Games.GetForChannel<TGame>(ctx.Channel.Id);
        }


        /// <summary>Attempts to create a <typeparamref name="TGame"/> for this context.</summary>
        public async Task RunMultiplayerGameAsync(CommandContext ctx, params DiscordUser[] players)
        {
            if (await CheckGameAlreadyExistsAsync(ctx)) return;

            var game = StartNewGame(await MultiplayerGame.CreateNewAsync<TGame>(ctx.Channel.Id, players, Services));

            while (await game.IsBotTurnAsync()) await game.BotInputAsync(); // When a bot starts

            await RespondGameAsync(ctx);
        }
    }
}
