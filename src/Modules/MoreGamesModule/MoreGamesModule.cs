using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Games;
using PacManBot.Utils;
using PacManBot.Services;
using PacManBot.Extensions;

namespace PacManBot.Modules
{
    [Name("👾More Games"), Remarks("3")]
    public partial class MoreGamesModule : ModuleBase<SocketCommandContext>
    {
        private readonly DiscordShardedClient shardedClient;
        private readonly LoggingService logger;
        private readonly StorageService storage;


        public MoreGamesModule(DiscordShardedClient shardedClient, LoggingService logger, StorageService storage)
        {
            this.shardedClient = shardedClient;
            this.logger = logger;
            this.storage = storage;
        }



        [Command("bump"), Alias("b", "refresh", "r", "move")]
        [Remarks("Move any game to the bottom of the chat")]
        [Summary("Moves the current game's message in this channel to the bottom of the chat, deleting the old one."
               + "This is useful if the game got lost in a sea of other messages, or if the game stopped responding")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks | ChannelPermission.AddReactions)]
        private async Task MoveGame()
        {
            var game = storage.GetGame(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync("There is no active game in this channel!", options: Bot.DefaultOptions);
                return;
            }

            try
            {
                var gameMessage = await game.GetMessage();
                if (gameMessage != null) await gameMessage.DeleteAsync(Bot.DefaultOptions); // Old message
            }
            catch (HttpException) { } // Something happened to the message, can ignore it

            var message = await ReplyAsync(game.GetContent(), false, game.GetEmbed()?.Build(), Bot.DefaultOptions);
            game.MessageId = message.Id;

            if (game is PacManGame pacManGame) await PacManModule.AddControls(pacManGame, message);
        }


        [Command("cancel"), Alias("end")]
        [Remarks("Cancel any game you're playing. Always usable by moderators")]
        [Summary("Cancels the current game in this channel, but only if you started or if nobody has played in over a minute. Always usable by users with the Manage Messages permission.")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks | ChannelPermission.AddReactions)]
        public async Task CancelGame()
        {
            var game = storage.GetGame(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync("There is no active game in this channel!", options: Bot.DefaultOptions);
                return;
            }

            if (game.UserId.Contains(Context.User.Id) || Context.UserCan(ChannelPermission.ManageMessages) || DateTime.Now - game.LastPlayed > TimeSpan.FromSeconds(60)
                || game is MultiplayerGame tpGame && tpGame.AllBots)
            {
                game.State = State.Cancelled;
                storage.DeleteGame(game);

                try
                {
                    var gameMessage = await game.GetMessage();
                    if (gameMessage != null)
                    {
                        await gameMessage.ModifyAsync(game.UpdateMessage, Bot.DefaultOptions);
                        if (game is PacManGame && Context.BotCan(ChannelPermission.ManageMessages)) await gameMessage.RemoveAllReactionsAsync(Bot.DefaultOptions);
                    }
                }
                catch (HttpException) { } // Something happened to the message, we can ignore it

                if (game is PacManGame pacManGame && Context.Guild != null)
                {
                    await ReplyAsync($"Game ended. Score won't be registered.\n**Result:** {pacManGame.score} points in {pacManGame.Time} turns", options: Bot.DefaultOptions);
                }
                else await Context.Message.AddReactionAsync(CustomEmoji.ECheck, Bot.DefaultOptions);
            }
            else await ReplyAsync("You can't cancel this game because someone else is still playing! Try again in a minute.", options: Bot.DefaultOptions);
        }
    }
}
