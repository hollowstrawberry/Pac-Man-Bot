using System;
using System.Text;
using Discord.Commands;

namespace PacManBot.CustomCommandAttributes
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
                if (attribute is HideHelp) Hidden = true;
                else if (attribute is Parameters parameters) Parameters = parameters.Value;
                else if (attribute is ExampleUsage usage) ExampleUsage = "{prefix}" + usage.Value.Replace("\n", "\n{prefix}");
            }

            if (Parameters == null)
            {
                var parsText = new StringBuilder();
                foreach (var parameter in command.Parameters)
                {
                    parsText.Append(parameter.IsOptional ? '[' : '<');
                    parsText.Append(parameter.Name);
                    parsText.Append(parameter.IsOptional ? ']' : '>');
                    parsText.Append(' ');
                }
                Parameters = parsText.ToString();
            }
        }
    }


    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    class HideHelp : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    class Parameters : Attribute
    {
        public string Value { get; private set; }
        public Parameters(string value)
        {
            Value = value;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    class ExampleUsage : Attribute
    {
        public string Value { get; private set; }
        public ExampleUsage(string value)
        {
            Value = value;
        }
    }
}
