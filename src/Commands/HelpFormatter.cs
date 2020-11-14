using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
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
            
            if (Command is null)
            {
                embed.WithTitle("PacMan Commands <a:pacman:409803570544902144>•••");

                if (Context.Guild is null) embed.WithDescription("No prefix needed in this channel!");
                else
                {
                    var storage = Context.Services.Get<DatabaseService>();
                    embed.WithDescription($"Command prefix: `{storage.GetGuildPrefix(Context.Guild)}`" +
                        $"\nNo prefix is needed in this channel!".If(!storage.RequiresPrefix(Context)));
                }

                var modules = Subcommands
                    .GroupBy(x => x.Module.ModuleType.GetCustomAttribute<CategoryAttribute>()?.Name)
                    .Where(x => x.Key is not null)
                    .OrderBy(x => Categories.Order.IndexOf(x.Key));
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
                    title.Append(arg.IsOptional ? ']' : '>');
                }
                title.Append('`');

                embed.WithTitle(title.ToString()).WithDescription(Command.Description);

                if (Command.ExecutionChecks.OfType<RequireOwnerAttribute>().Any())
                    embed.AddField($"{CustomEmoji.Staff} Developer", "You can't use it!", true);
                else if (Command.IsHidden)
                    embed.AddField("👻 Hidden", "How did you find it?", true);

                var userPerms = Command.ExecutionChecks.OfType<RequireUserPermissionsAttribute>().FirstOrDefault();
                if (userPerms is not null)
                    embed.AddField("Requires Permissions", userPerms.Permissions.ToPermissionString(), true);
                if (Command.Aliases.Any())
                    embed.AddField("Aliases", Command.Aliases.Select(x => $"`{x}`").JoinString(", "), true);
                if (Subcommands is not null)
                    embed.AddField("Subcommands", Subcommands.Select(x => $"`{x.QualifiedName}`").JoinString(", "), true);
            }

            return new CommandHelpMessage(null, embed.Build());
        }
    }
}
