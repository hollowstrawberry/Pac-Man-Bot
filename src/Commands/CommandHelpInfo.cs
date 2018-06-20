using System.Text;
using Discord.Commands;

namespace PacManBot.Commands
{
    public class CommandHelpInfo
    {
        public bool Hidden { get; }
        public string Remarks { get; }
        public string Summary { get; }
        public string Parameters { get; }
        public string ExampleUsage { get; }


        public CommandHelpInfo(CommandInfo command)
        {
            Hidden = false;
            Remarks = command.Remarks ?? "";
            Summary = command.Summary ?? "";
            ExampleUsage = "";
            Parameters = null;

            foreach (var attribute in command.Attributes)
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
                foreach (var parameter in command.Parameters)
                {
                    paramsText.Append(parameter.IsOptional ? $"[{parameter.Name}] " : $"<{parameter.Name}> ");
                }
                Parameters = paramsText.ToString();
            }
        }
    }
}
