using System;
using System.Text;
using System.Threading.Tasks;
using Discord;
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
                    paramsText.Append(parameter.IsOptional ? $"[{parameter.Name}] " : $"<{parameter.Name}> ");
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


    // The original preconditions just... didn't work?
    abstract class BaseBetterRequirePermission : PreconditionAttribute
    {
        protected GuildPermission? guildPerms = null;
        protected ChannelPermission? channelPerms = null;


        public BaseBetterRequirePermission(ChannelPermission channelPerms)
        {
            this.channelPerms = channelPerms;
        }

        public BaseBetterRequirePermission(GuildPermission guildPerms)
        {
            this.guildPerms = guildPerms;
        }


        protected PreconditionResult CheckPermissions(ICommandContext context, CommandInfo command, IGuildUser user, string name)
        {
            if (guildPerms != null)
            {
                if (user == null) return PreconditionResult.FromError($"{name} requires guild permissions but is not in a guild");
                GuildPermission currentPerms = (GuildPermission)user.GuildPermissions.RawValue;

                if (currentPerms.HasFlag(guildPerms)) return PreconditionResult.FromSuccess();
                else return PreconditionResult.FromError($"{name} requires guild permission {(guildPerms ^ currentPerms) & guildPerms}");
            }
            else
            {
                ChannelPermission currentPerms;
                if (user == null) currentPerms = Utils.CorrectDmPermissions; // They got these wrong in the library
                else currentPerms = (ChannelPermission)user.GetPermissions(context.Channel as IGuildChannel).RawValue;

                if (currentPerms.HasFlag(channelPerms)) return PreconditionResult.FromSuccess();
                else return PreconditionResult.FromError($"{name} requires guild permission {(channelPerms ^ currentPerms) & channelPerms}");
            }
        }
    }


    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    class BetterRequireBotPermission : BaseBetterRequirePermission
    {
        public BetterRequireBotPermission(ChannelPermission channelPerms) : base(channelPerms) { }
        public BetterRequireBotPermission(GuildPermission guildPerms) : base(guildPerms) { }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
            => CheckPermissions(context, command, context.Guild == null ? null : await context.Guild.GetCurrentUserAsync(), "Bot");
    }


    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    class BetterRequireUserPermission : BaseBetterRequirePermission
    {
        public BetterRequireUserPermission(ChannelPermission channelPerms) : base(channelPerms) { }
        public BetterRequireUserPermission(GuildPermission guildPerms) : base(guildPerms) { }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
            => CheckPermissions(context, command, context.User as IGuildUser, "User");
    }
}
