using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PacManBot.Extensions;

namespace PacManBot.Commands
{
    /// <summary>
    /// When present, indicates that this command should not be visible.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class HideHelpAttribute : Attribute { }


    /// <summary>
    /// Overrides the automatic parameters displayed for this command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ParametersAttribute : Attribute
    {
        public string Value { get; }
        public ParametersAttribute(string value)
        {
            Value = value;
        }
    }


    /// <summary>
    /// Provides example usage of a command. Use "{prefix}" to refer to the guild prefix.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ExampleUsageAttribute : Attribute
    {
        public string Value { get; }
        public ExampleUsageAttribute(string value)
        {
            Value = value;
        }
    }




    /// <summary>
    /// The original preconditions from Discord.Net just didn't work for some reason.
    /// </summary>
    public abstract class BasePermissionAttribute : PreconditionAttribute
    {
        protected GuildPermission? guildPerms;
        protected ChannelPermission? channelPerms;


        protected BasePermissionAttribute(ChannelPermission channelPerms)
        {
            this.channelPerms = channelPerms;
        }

        protected BasePermissionAttribute(GuildPermission guildPerms)
        {
            this.guildPerms = guildPerms;
        }


        protected PreconditionResult CheckPermissions(ICommandContext context, CommandInfo command, IGuildUser user, string name)
        {
            if (guildPerms.HasValue)
            {
                if (user == null) return PreconditionResult.FromError($"{name} requires guild permissions but is not in a guild");
                var currentPerms = (GuildPermission)user.GuildPermissions.RawValue;

                return currentPerms.HasFlag(guildPerms)
                    ? PreconditionResult.FromSuccess()                                 //This gets the perms that are missing
                    : PreconditionResult.FromError($"{name} requires guild permission {(guildPerms ^ currentPerms) & guildPerms}");
            }
            else
            {
                var currentPerms = user == null
                    ? DiscordExtensions.CorrectDmPermissions // They got these wrong in the library
                    : (ChannelPermission)user.GetPermissions(context.Channel as IGuildChannel).RawValue;

                return currentPerms.HasFlag(channelPerms)
                    ? PreconditionResult.FromSuccess()
                    : PreconditionResult.FromError($"{name} requires guild permission {(channelPerms ^ currentPerms) & channelPerms}");
            }
        }
    }


    /// <summary>
    /// Indicates this command can't run unless the bot has the specified permissions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class BetterRequireBotPermissionAttribute : BasePermissionAttribute
    {
        public BetterRequireBotPermissionAttribute(ChannelPermission channelPerms) : base(channelPerms) { }
        public BetterRequireBotPermissionAttribute(GuildPermission guildPerms) : base(guildPerms) { }


        public override async Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            return CheckPermissions(context, command, context.Guild == null ? null : await context.Guild.GetCurrentUserAsync(), "Bot");
        }
    }


    /// <summary>
    /// Indicates this command can't run unless the user calling it has the specified permissions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class BetterRequireUserPermissionAttribute : BasePermissionAttribute
    {
        public BetterRequireUserPermissionAttribute(ChannelPermission channelPerms) : base(channelPerms) { }
        public BetterRequireUserPermissionAttribute(GuildPermission guildPerms) : base(guildPerms) { }


        public override Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            return Task.FromResult(CheckPermissions(context, command, context.User as IGuildUser, "User"));
        }
    }
}
