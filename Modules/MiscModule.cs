using Discord;
using Discord.Commands;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace PacManBot.Modules
{
    [Name("Misc")]
    public class MiscModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService service;
        private readonly IConfigurationRoot config;

        public MiscModule(CommandService service, IConfigurationRoot config)
        {
            this.service = service;
            this.config = config;
        }

        [Command("help"), Alias("h"), Summary("List of commands")]
        public async Task HelpAsync()
        {
            string prefix = config["prefix"];
            var embed = new EmbedBuilder() { Color = new Color(241, 195, 15) }; //Create a new embed block

            foreach (var module in service.Modules) //Go through all modules
            {
                string commandList = null; //Text under the module title in the embed block

                foreach (var command in module.Commands) //Go through all commands
                {
                    var result = await command.CheckPreconditionsAsync(Context); //Only show commands the user can use
                    if (result.IsSuccess)
                    {
                        for (int i = 0; i < command.Aliases.Count; i++) //Lists command name and aliases
                        {
                            commandList += (i > 0 ? ", " : "") + $"**{command.Aliases[i]}**";
                        }

                        if (!string.IsNullOrEmpty(command.Summary)) //Adds the command summary
                        {
                            commandList += $" - *{command.Summary}*";
                        }

                        commandList += "\n";
                    }
                }

                if (!string.IsNullOrWhiteSpace(commandList))
                {
                    embed.AddField(f =>
                    {
                        f.Name = module.Name;
                        f.Value = commandList;
                        f.IsInline = false;
                    });
                }
            }

            await ReplyAsync("", false, embed.Build()); //Send the built embed
        }

        [Command("waka"), Summary("Waka.")]
        public Task Ping() => ReplyAsync("waka");

        [Command("say"), Summary("Make the bot say anything (Moderator)")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task Say([Remainder]string text) => await ReplyAsync(text);

        [Command("clear"), Alias("c"), Summary("Clear messages from this bot. You can specify an amount (Moderator)")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task ClearGameMessages(int amount = 10)
        {
            var messages = await Context.Channel.GetMessagesAsync(amount).Flatten();
            foreach (IMessage message in messages)
            {
                if (message.Author.Id == Context.Client.CurrentUser.Id) await message.DeleteAsync(); //Remove all messages from this bot
            }
        }

        [Command("invite"), Alias("inv"), Summary("Invite this bot to your server")]
        public async Task SayBotInvite() => await ReplyAsync("Invite this bot into a server you own: <http://bit.ly/pacmanbotdiscord>");

        [Command("about"), Summary("About this bot")]
        public async Task SayBotInfo() => await ReplyAsync("🎮 Play a turn-based version of Pac-Man on a Discord chat, with non-spammy reaction-based controls. Designed by Samrux#3980 as a test project to learn the Discord API and other stuff.\nInvite the bot to your server: <http://bit.ly/pacmanbotdiscord>\nGitHub: <https://github.com/Samrux/Pac-Man-Bot>\nEmail: samruxb@gmail.com");

    }
}
