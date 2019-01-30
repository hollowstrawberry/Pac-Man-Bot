using System;
using System.Threading.Tasks;
using Discord.Net;
using Discord.WebSocket;
using PacManBot.Games;

namespace PacManBot.Commands.Modules
{
    /// <summary>A module equipped to run multiplayer games.</summary>
    public abstract class MultiplayerGameModule<TGame> : BaseGameModule<TGame>
        where TGame : MultiplayerGame
    {
        /// <summary>Attempts to create a <see cref="TGame"/> for this context.</summary>
        public async Task RunGameAsync(params SocketUser[] players)
        {
            if (await CheckGameAlreadyExistsAsync()) return;

            StartNewGame(await MultiplayerGame.CreateNewAsync<TGame>(Context.Channel.Id, players, Services));

            while (!Game.AllBots && Game.BotTurn) Game.BotInput(); // When a bot starts

            var msg = await ReplyGameAsync();

            if (Game.AllBots)
            {
                while (Game.State == State.Active)
                {
                    try
                    {
                        Game.BotInput();
                        msg = await UpdateGameMessageAsync();
                        if (msg == null) Game.State = State.Cancelled;
                    }
                    catch (OperationCanceledException) { }
                    catch (TimeoutException) { }
                    catch (HttpException) { }  // All of these are connection-related and ignorable in this situation

                    await Task.Delay(Program.Random.Next(2500, 4001));
                }

                RemoveGame();
            }
        }
    }
}
