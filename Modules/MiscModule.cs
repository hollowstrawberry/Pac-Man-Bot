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
                string description = null; //Text under the module title in the embed block

                foreach (var command in module.Commands) //Go througyh all commands
                {
                    var result = await command.CheckPreconditionsAsync(Context);
                    if (result.IsSuccess)
                    {
                        for (int i = 0; i < command.Aliases.Count; i++) //Lists command name and aliases
                        {
                            description += (i > 0 ? ", " : "") + $"**{command.Aliases[i]}**";
                        }

                        if (!string.IsNullOrEmpty(command.Summary)) //Adds the command summary
                        {
                            description += $" - *{command.Summary}*";
                        }

                        description += "\n";
                    }
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    embed.AddField(f =>
                    {
                        f.Name = module.Name;
                        f.Value = description;
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
        public Task Say([Remainder]string text) => ReplyAsync(text);
    }
}
