using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace PacManBot.Services
{
    class ReactionHandler
    {
        private readonly DiscordSocketClient _client;

        //DiscordSocketClient is injected automatically from the IServiceProvider
        public ReactionHandler(DiscordSocketClient client)
        {
            _client = client;

            _client.ReactionAdded += OnReactionAdded; //Event
        }


        private Task OnReactionAdded(Cacheable<IUserMessage, ulong> messageData, ISocketMessageChannel channel, SocketReaction reaction)
        {
            ulong botID = _client.CurrentUser.Id;

            if (messageData.HasValue && reaction.User.IsSpecified && //Ignores events it can't use
                messageData.Value.Author.Id == botID && //Only uses reactions to its own messages
                reaction.UserId != botID //Ignores its own reactions
            ){
                Task.Run(async () => //Wrapping in a Task.Run prevents the gateway from getting blocked in case something goes wrong
                {
                    var context = new SocketCommandContext(_client, messageData.Value as SocketUserMessage);
                    await Modules.PacManModule.Controls.ExecuteInput(context, reaction);
                });
            }

            return Task.CompletedTask;
        }
    }
}
