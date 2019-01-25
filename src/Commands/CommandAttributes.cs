using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PacManBot.Extensions;

namespace PacManBot.Commands
{
    /// <summary>
    /// When present, indicates that this command should not be visible when listing commands to the user.
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
    /// Provides example usage of a command. The string "{prefix}" refers to the guild prefix.
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
    /// Allows either the application owner or any specified developers in the configuration file to run a command.
    /// </summary>
    public class RequireDeveloperAttribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var config = services.Get<PmConfig>();
            var app = await context.Client.GetApplicationInfoAsync(PmBot.DefaultOptions);
            ulong userId = context.User.Id;

            bool success = app?.Owner?.Id == userId || config.developers.Contains(userId);
            return success ? PreconditionResult.FromSuccess() : PreconditionResult.FromError("User must be an owner or developer.");
        }
    }


    /// <summary>
    /// The original permission preconditions from Discord.Net had a wrong value for DM permissions,
    /// and didn't list all missing permissions in the error message. I hope to delete this one day.
    /// </summary>
    public abstract class PmBasePermissionAttribute : PreconditionAttribute
    {
        protected GuildPermission? guildPerms;
        protected ChannelPermission? channelPerms;


        protected PmBasePermissionAttribute(ChannelPermission channelPerms)
        {
            this.channelPerms = channelPerms;
        }

        protected PmBasePermissionAttribute(GuildPermission guildPerms)
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
                    ? DiscordExtensions.DmPermissions
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
    public class PmRequireBotPermissionAttribute : PmBasePermissionAttribute
    {
        public PmRequireBotPermissionAttribute(ChannelPermission channelPerms) : base(channelPerms) { }
        public PmRequireBotPermissionAttribute(GuildPermission guildPerms) : base(guildPerms) { }


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
    public class PmRequireUserPermissionAttribute : PmBasePermissionAttribute
    {
        public PmRequireUserPermissionAttribute(ChannelPermission channelPerms) : base(channelPerms) { }
        public PmRequireUserPermissionAttribute(GuildPermission guildPerms) : base(guildPerms) { }


        public override Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            return Task.FromResult(CheckPermissions(context, command, context.User as IGuildUser, "User"));
        }
    }
}
