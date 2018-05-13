using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Games;
using PacManBot.Services;
using PacManBot.Constants;
using static PacManBot.Games.GameUtils;

namespace PacManBot.Modules
{
    [Name("🎮Other Games")]
    public class OtherGamesModule : ModuleBase<SocketCommandContext>
    {
        private readonly DiscordShardedClient shardedClient;
        private readonly LoggingService logger;
        private readonly StorageService storage;


        public OtherGamesModule(DiscordShardedClient shardedClient, LoggingService logger, StorageService storage)
        {
            this.shardedClient = shardedClient;
            this.logger = logger;
            this.storage = storage;
        }



        [Command("tictactoe"), Alias("ttt")]
        public async Task StartTicTacToe(SocketGuildUser opponent)
        {
            await StartGame<TTTGame>(opponent);
        }



        private async Task StartGame<T>(SocketGuildUser opponent) where T : GameInstance
        {
            var players = new ulong[] { opponent.Id, Context.User.Id };

            GameInstance game;
            if (typeof(T) == typeof(TTTGame)) game = new TTTGame(Context.Channel.Id, players, shardedClient, logger, storage);
            else throw new NotImplementedException();

            try
            {
                var message = await ReplyAsync(game.GetContent(), false, game.GetEmbed().Build(), Utils.DefaultRequestOptions);
                game.messageId = message.Id;
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Critical, $"{e}");
            }
            
            storage.AddGame(game);
        }
    }
}
