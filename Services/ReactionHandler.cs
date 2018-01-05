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
        private readonly CommandService commands;
        private readonly IConfigurationRoot config;
        private readonly IServiceProvider provider;

        //DiscordSocketClient, CommandService, IConfigurationRoot, and IServiceProvider are injected automatically from the IServiceProvider
        public ReactionHandler(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config, IServiceProvider provider)
        {
            this.discord = discord;
            this.config = config;

            this.discord.ReactionAdded += OnReactionAdded;
        }

        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!messageData.HasValue || !reaction.User.IsSpecified) return;
            if (reaction.UserId == discord.CurrentUser.Id) return; //Ignores itself

            await Modules.PacManModule.Controls.OnReactionAdded(messageData.Value, reaction);
        }


    }
}
