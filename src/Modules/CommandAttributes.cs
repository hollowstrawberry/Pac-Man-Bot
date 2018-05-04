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


    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    class RequireBotPermissionImproved : PreconditionAttribute
    {
        GuildPermission? guildPerms = null;
        ChannelPermission? channelPerms = null;


        public RequireBotPermissionImproved(ChannelPermission channelPerms)
        {
            this.channelPerms = channelPerms;
        }

        public RequireBotPermissionImproved(GuildPermission guildPerms)
        {
            this.guildPerms = guildPerms;
        }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (guildPerms != null)
            {
                if (context.Guild == null) return PreconditionResult.FromError("Bot requires guild permissions");
                GuildPermission guildPermissions = (GuildPermission)(await context.Guild.GetCurrentUserAsync()).GuildPermissions.RawValue;
                return guildPermissions.HasFlag(guildPerms) ? PreconditionResult.FromSuccess() : PreconditionResult.FromError($"Bot requires guild permission {(guildPerms ^ guildPermissions) & guildPerms}");
            }

            ChannelPermission channelPermissions;
            if (context.Guild == null) channelPermissions = Utils.CorrectDmPermissions;
            else channelPermissions = (ChannelPermission)(await context.Guild.GetCurrentUserAsync()).GetPermissions(context.Channel as IGuildChannel).RawValue;

            return channelPermissions.HasFlag(channelPerms) ? PreconditionResult.FromSuccess() : PreconditionResult.FromError($"Bot requires channel permission {(channelPerms ^ channelPermissions) & channelPerms}");
        }
    }


    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    class RequireUserPermissionImproved : PreconditionAttribute
    {
        GuildPermission? guildPerms = null;
        ChannelPermission? channelPerms = null;


        public RequireUserPermissionImproved(ChannelPermission channelPerms)
        {
            this.channelPerms = channelPerms;
        }

        public RequireUserPermissionImproved(GuildPermission guildPerms)
        {
            this.guildPerms = guildPerms;
        }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (guildPerms != null)
            {
                if (context.Guild == null) return PreconditionResult.FromError("User requires guild permissions");
                GuildPermission guildPermissions = (GuildPermission)(context.User as IGuildUser).GuildPermissions.RawValue;
                return guildPermissions.HasFlag(guildPerms) ? PreconditionResult.FromSuccess() : PreconditionResult.FromError($"User requires guild permission {(guildPerms ^ guildPermissions) & guildPerms}");
            }

            ChannelPermission channelPermissions;
            if (context.Guild == null) channelPermissions = Utils.CorrectDmPermissions;
            else channelPermissions = (ChannelPermission)(context.User as IGuildUser).GetPermissions(context.Channel as IGuildChannel).RawValue;

            return channelPermissions.HasFlag(channelPerms) ? PreconditionResult.FromSuccess() : PreconditionResult.FromError($"User requires channel permission {(channelPerms ^ channelPermissions) & channelPerms}");
        }
    }
}
