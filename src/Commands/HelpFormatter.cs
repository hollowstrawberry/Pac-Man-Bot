using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Converters;
using DSharpPlus.CommandsNext.Entities;
using DSharpPlus.Entities;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Services;

namespace PacManBot.Commands
{
    public class HelpFormatter : BaseHelpFormatter
    {
        public Command Command { get; private set; }
        public Command[] Subcommands { get; private set; }

        public HelpFormatter(CommandContext ctx) : base(ctx)
        {
            Command = null;
            Subcommands = null;
        }

        public override BaseHelpFormatter WithCommand(Command command)
        {
            Command = command;
            return this;
        }

        public override BaseHelpFormatter WithSubcommands(IEnumerable<Command> subcommands)
        {
            Subcommands = subcommands.ToArray();
            return this;
        }


        public override CommandHelpMessage Build()
        {
            var embed = new DiscordEmbedBuilder().WithColor(Colors.PacManYellow);
            
            if (Command == null)
            {
                embed.WithTitle("<a:pacman:409803570544902144> PacMan Commands");

                if (Context.Guild == null) embed.WithDescription("No prefix needed in this channel!");
                else
                {
                    var storage = Context.Services.Get<StorageService>();
                    embed.WithDescription($"Command prefix: `{storage.GetGuildPrefix(Context.Guild)}`" +
                        $"\nNo prefix is needed in this channel!".If(!storage.RequiresPrefix(Context)));
                }

                var modules = Subcommands
                    .GroupBy(x => x.Module.ModuleType.GetCustomAttribute<ModuleAttribute>()?.Name)
                    .Where(x => x.Key != null)
                    .OrderBy(x => ModuleNames.Order.IndexOf(x.Key));
                foreach (var commands in modules)
                {
                    embed.AddField(commands.Key, commands.Select(x => $"`{x.Name}`").JoinString(", "), false);
                }
            }
            else
            {
                var args = Command.Overloads.OrderByDescending(x => x.Priority).First().Arguments;
                var title = new StringBuilder($"Command `{Command.QualifiedName}");
                foreach (var arg in args)
                {
                    title.Append(arg.IsOptional ? " [" : " <");
                    title.Append(arg.Name);
                    title.Append(arg.IsOptional ? "]" : ">");
                }
                title.Append("`");

                embed.WithTitle(title.ToString()).WithDescription(Command.Description);

                if (Command.Aliases.Any())
                    embed.AddField("Aliases", Command.Aliases.Select(x => $"`{x}`").JoinString(", "), true);
                if (Subcommands != null)
                    embed.AddField("Subcommands", Subcommands.Select(x => $"`{x.QualifiedName}`").JoinString(", "), true);
            }

            return new CommandHelpMessage(null, embed.Build());
        }
    }
}
