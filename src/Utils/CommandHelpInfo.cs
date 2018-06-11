using System.Text;
using Discord.Commands;

namespace PacManBot.Utils
{
    public class CommandHelpInfo
    {
        public bool Hidden { get; private set; }
        public string Remarks { get; private set; }
        public string Summary { get; private set; }
        public string Parameters { get; private set; }
        public string ExampleUsage { get; private set; }


        public CommandHelpInfo(CommandInfo command)
        {
            Hidden = false;
            Remarks = command.Remarks ?? "";
            Summary = command.Summary ?? "";
            ExampleUsage = "";
            Parameters = null;

            foreach (var attribute in command.Attributes)
            {
                if (attribute is HideHelpAttribute) Hidden = true;
                else if (attribute is ParametersAttribute parameters) Parameters = parameters.Value;
                else if (attribute is ExampleUsageAttribute usage) ExampleUsage = "{prefix}" + usage.Value.Replace("\n", "\n{prefix}");
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
