using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

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


        private Task OnReactionAdded(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!messageData.HasValue || !reaction.User.IsSpecified) return Task.CompletedTask;
            if (reaction.UserId == discord.CurrentUser.Id) return Task.CompletedTask; //Ignores itself

            Task.Run(async () => //Wrapping in a Task.Run prevents the gateway from getting blocked in case something goes wrong
            {
                var context = new SocketCommandContext(discord, messageData.Value as SocketUserMessage);
                await Modules.PacManModule.Controls.OnReactionAdded(context, reaction);
            });
            return Task.CompletedTask;
        }
    }
}
