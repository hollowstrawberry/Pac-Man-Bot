using System.Linq;
using System.Text;
using Discord;
using Discord.Commands;
using PacManBot.Commands;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Utils
{
    /// <summary>
    /// Wrapper that builds user help for a command.
    /// </summary>
    public class CommandHelp
    {
        public static readonly Color EmbedColor = Colors.PacManYellow;

        public CommandInfo Command { get; }
        public bool Hidden { get; }
        public string Remarks { get; }
        public string[] Summary { get; }
        public string Parameters { get; }
        public string ExampleUsage { get; }

        private readonly EmbedFieldBuilder[] embedFields;


        public CommandHelp(CommandInfo command)
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


            var embed = new EmbedBuilder(); // Temporary for nicer syntax

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
            
            foreach (var field in embed.Fields.Take(embed.Fields.Count - 1))
            {
                field.Value = field.Value.ToString() + "\nᅠ"; // Padding
            }

            embedFields = embed.Fields.ToArray();
        }


        /// <summary>Creates an embed containing the user manual for this command.</summary>
        public EmbedBuilder GetEmbed(string prefix)
        {
            var embed = new EmbedBuilder
            {
                Title = $"__Command__: {prefix}{Command.Name}",
                Color = EmbedColor,
            };

            foreach (var field in embedFields)
            {
                embed.AddField(field.Name, field.Value.ToString().Replace("{prefix}", prefix), field.IsInline);
            }

            return embed;
        }
    }
}
