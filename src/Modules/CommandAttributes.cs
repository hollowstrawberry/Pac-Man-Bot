using System;
using System.Text;
using Discord.Commands;

namespace PacManBot.Modules
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
                var paramsText = new StringBuilder();
                foreach (var parameter in command.Parameters)
                {
                    paramsText.Append(parameter.IsOptional ? '[' : '<');
                    paramsText.Append(parameter.Name);
                    paramsText.Append(parameter.IsOptional ? ']' : '>');
                    paramsText.Append(' ');
                }
                Parameters = paramsText.ToString();
            }
        }
    }



    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    class HideHelp : Attribute { }


    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    class Parameters : Attribute
    {
        public string Value { get; private set; }
        public Parameters(string value)
        {
            Value = value;
        }
    }


    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    class ExampleUsage : Attribute
    {
        public string Value { get; private set; }
        public ExampleUsage(string value)
        {
            Value = value;
        }
    }
}
