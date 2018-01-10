using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace PacManBot.Services
{
    class ReactionHandler
    {
        private readonly DiscordSocketClient discord;

        //DiscordSocketClient is injected automatically from the IServiceProvider
        public ReactionHandler(DiscordSocketClient discord)
        {
            this.discord = discord;

            this.discord.ReactionAdded += OnReactionAdded; //Event
        }


        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!messageData.HasValue || !reaction.User.IsSpecified) return;
            if (reaction.UserId == discord.CurrentUser.Id) return; //Ignores itself

            await Modules.PacManModule.Controls.OnReactionAdded(messageData.Value, reaction);
        }
    }
}
