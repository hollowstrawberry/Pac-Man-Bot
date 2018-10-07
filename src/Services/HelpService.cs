using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Discord;
using Discord.Commands;
using PacManBot.Commands;
using PacManBot.Constants;
using PacManBot.Extensions;
using System.Threading.Tasks;

namespace PacManBot.Services
{
    /// <summary>
    /// Provides help information for commands.
    /// </summary>
    public class HelpService
    {
        private readonly IServiceProvider services;
        private readonly CommandService commands;
        private readonly StorageService storage;

        private readonly Color embedColor = Colors.PacManYellow;

        private ConcurrentDictionary<string, CommandHelpInfo> internalHelpInfo;
        private ConcurrentDictionary<string, CommandHelpInfo> HelpInfo
            => internalHelpInfo ?? (internalHelpInfo = BuildHelpInfo());


        /// <summary>
        /// Gathers custom information about a command.
        /// </summary>
        private class CommandHelpInfo
        {
            public CommandInfo Command { get; }
            public EmbedFieldBuilder[] Fields { get; }

            public bool Hidden { get; }
            public string Remarks { get; }
            public string[] Summary { get; }
            public string Parameters { get; }
            public string ExampleUsage { get; }

            public CommandHelpInfo(CommandInfo command)
            {
                Command = command;

                Hidden = false;
                Remarks = Command.Remarks ?? "";
                ExampleUsage = "";
                Parameters = null;

                Summary = new string[0];
                if (!string.IsNullOrWhiteSpace(Command.Summary))
                {
                    Summary = Command.Summary.Split("\n\n\n");
                }

                foreach (var attribute in Command.Attributes)
                {
                    switch (attribute)
                    {
                        case HideHelpAttribute _:
                            Hidden = true;
                            break;
                        case ParametersAttribute parameters:
                            Parameters = parameters.Value;
                            break;
                        case ExampleUsageAttribute usage:
                            ExampleUsage = "{prefix}" + usage.Value.Replace("\n", "\n{prefix}");
                            break;
                    }
                }

                if (Parameters == null)
                {
                    var paramsText = new StringBuilder();
                    foreach (var parameter in Command.Parameters)
                    {
                        paramsText.Append(parameter.IsOptional ? $"[{parameter.Name}] " : $"<{parameter.Name}> ");
                    }
                    Parameters = paramsText.ToString();
                }

                Fields = MakeFields();
            }

            private EmbedFieldBuilder[] MakeFields()
            {
                var embed = new EmbedBuilder();

                if (Hidden)
                {
                    embed.AddField("Hidden command", "*Are you a wizard?*", true);
                }

                if (Parameters != "")
                {
                    embed.AddField("Parameters", Parameters, true);
                }

                if (Command.Aliases.Count > 1)
                {
                    string aliases = Command.Aliases.Skip(1).Select(x => $"{{prefix}}{x}").JoinString(", ");
                    embed.AddField("Aliases", aliases, true);
                }

                if (Summary.Length > 0)
                {
                    for (int i = 0; i < Summary.Length; i++)
                    {
                        embed.AddField("Summary" + $" #{i + 1}".If(Summary.Length > 1), Summary[i].Truncate(1024));
                    }
                }

                if (ExampleUsage != "")
                {
                    embed.AddField("Example Usage", ExampleUsage);
                }

                // Padding
                foreach (var field in embed.Fields.Take(embed.Fields.Count - 1))
                {
                    field.Value = field.Value.ToString() + "\nᅠ";
                }

                return embed.Fields.ToArray();
            }
        }




        public HelpService(IServiceProvider services)
        {
            this.services = services;
            commands = services.Get<CommandService>();
            storage = services.Get<StorageService>();
        }


        private ConcurrentDictionary<string, CommandHelpInfo> BuildHelpInfo()
        {
            var dict = new ConcurrentDictionary<string, CommandHelpInfo>();

            foreach (var com in commands.Commands.OrderByDescending(c => c.Priority))
            {
                var help = new CommandHelpInfo(com);
                foreach (var alias in com.Aliases)
                {
                    string key = alias.ToLower();
                    if (!dict.ContainsKey(key)) dict[key] = help;
                }
            }

            return dict;
        }



        public EmbedBuilder MakeHelp(string commandName, string prefix = "")
        {
            if (!HelpInfo.TryGetValue(commandName.ToLower(), out var help)) return null;

            var embed = new EmbedBuilder
            {
                Title = $"__Command__: {prefix}{help.Command.Name}",
                Color = embedColor,
            };

            foreach (var field in help.Fields)
            {
                embed.AddField(field.Name, field.Value.ToString().Replace("{prefix}", prefix), field.IsInline);
            }

            return embed;
        }


        public async Task<EmbedBuilder> MakeAllHelp(ICommandContext context, bool expanded)
        {
            string prefix = storage.GetPrefix(context);

            var embed = new EmbedBuilder()
            {
                Title = $"{CustomEmoji.PacMan} __**Bot Commands**__",

                Description = (prefix == "" ? "No prefix is needed in this channel!" : $"Prefix for this server is '{prefix}'")
                            + $"\nYou can do **{prefix}help command** for more information about a command.\n\n"
                            + $"Parameters: [optional] <needed>".If(expanded),

                Color = embedColor,
            };

            foreach (var module in commands.Modules.OrderBy(m => m.Remarks))
            {
                var moduleText = new StringBuilder();

                foreach (var command in module.Commands.OrderByDescending(c => c.Priority).Distinct(CommandEqComp.Instance))
                {
                    var help = HelpInfo[command.Name.ToLower()];

                    if (!help.Hidden)
                    {
                        var conditions = await command.CheckPreconditionsAsync(context, services);
                        if (!conditions.IsSuccess) continue;

                        if (expanded)
                        {
                            moduleText.Append($"**{command.Name} {help.Parameters}**");
                            if (help.Remarks != "") moduleText.Append($" — *{help.Remarks}*");
                            moduleText.Append("\n");
                        }
                        else
                        {
                            moduleText.Append($"**{command.Name}**, ");
                        }
                    }
                }

                if (!expanded && module.Name.Contains("Pac-Man")) moduleText.Append("**bump**, **cancel**");

                if (moduleText.Length > 0)
                {
                    embed.AddField(module.Name, moduleText.ToString().Trim(' ', ',', '\n'));
                }
            }

            return embed;
        }


        private class CommandEqComp : IEqualityComparer<CommandInfo>
        {
            public static CommandEqComp Instance = new CommandEqComp();

            public bool Equals(CommandInfo x, CommandInfo y) => x.Name == y.Name;
            public int GetHashCode(CommandInfo obj) => obj.Name.GetHashCode();
        }
    }
}
