using System;
using System.Threading.Tasks;
using Discord.Net;
using Discord.WebSocket;
using PacManBot.Games;

namespace PacManBot.Commands
{
    /// <summary>A module equipped to run multiplayer games.</summary>
    public abstract class MultiplayerGameModule<TGame> : BaseGameModule<TGame>
        where TGame : MultiplayerGame
    {
        protected override TGame GetCurrentGame()
        {
            return Games.GetForChannel<TGame>(Context.Channel.Id);
        }



        /// <summary>Attempts to create a <typeparamref name="TGame"/> for this context.</summary>
        public async Task RunGameAsync(params SocketUser[] players)
        {
            if (await CheckGameAlreadyExistsAsync()) return;

            StartNewGame(await MultiplayerGame.CreateNewAsync<TGame>(Context.Channel.Id, players, Services));

            while (!Game.AllBots && Game.BotTurn) await Game.BotInputAsync(); // When a bot starts

            var msg = await ReplyGameAsync();

            if (Game.AllBots) // Bot loop
            {
                while (Game.State == GameState.Active)
                {
                    try
                    {
                        await Game.BotInputAsync();
                        msg = await UpdateGameMessageAsync();
                        if (msg == null) break;
                    }
                    catch (OperationCanceledException) { }
                    catch (TimeoutException) { }
                    catch (HttpException) { }  // All of these are connection-related and ignorable in this situation

                    await Task.Delay(Program.Random.Next(2500, 4001));
                }

                EndGame();
            }
        }
    }
}
