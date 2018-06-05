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



    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    class HideHelpAttribute : Attribute { }


    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    class ParametersAttribute : Attribute
    {
        public string Value { get; private set; }
        public ParametersAttribute(string value)
        {
            Value = value;
        }
    }


    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    class ExampleUsageAttribute : Attribute
    {
        public string Value { get; private set; }
        public ExampleUsageAttribute(string value)
        {
            Value = value;
        }
    }


    // The original preconditions just... didn't work?
    abstract class BaseBetterRequirePermissionAttribute : PreconditionAttribute
    {
        protected GuildPermission? guildPerms = null;
        protected ChannelPermission? channelPerms = null;


        public BaseBetterRequirePermissionAttribute(ChannelPermission channelPerms)
        {
            this.channelPerms = channelPerms;
        }

        public BaseBetterRequirePermissionAttribute(GuildPermission guildPerms)
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
    class BetterRequireBotPermissionAttribute : BaseBetterRequirePermissionAttribute
    {
        public BetterRequireBotPermissionAttribute(ChannelPermission channelPerms) : base(channelPerms) { }
        public BetterRequireBotPermissionAttribute(GuildPermission guildPerms) : base(guildPerms) { }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
            => CheckPermissions(context, command, context.Guild == null ? null : await context.Guild.GetCurrentUserAsync(), "Bot");
    }


    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    class BetterRequireUserPermissionAttribute : BaseBetterRequirePermissionAttribute
    {
        public BetterRequireUserPermissionAttribute(ChannelPermission channelPerms) : base(channelPerms) { }
        public BetterRequireUserPermissionAttribute(GuildPermission guildPerms) : base(guildPerms) { }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            await Task.Delay(0); // Now please shut up, compiler.
            return CheckPermissions(context, command, context.User as IGuildUser, "User");
        }
    }
}
