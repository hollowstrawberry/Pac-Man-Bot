using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord;
using Discord.Commands;
using PacManBot.Extensions;

namespace PacManBot.Utils
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    class HideHelpAttribute : Attribute
    {
    }


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
    abstract class BasePermissionAttribute : PreconditionAttribute
    {
        protected GuildPermission? guildPerms = null;
        protected ChannelPermission? channelPerms = null;


        public BasePermissionAttribute(ChannelPermission channelPerms)
        {
            this.channelPerms = channelPerms;
        }

        public BasePermissionAttribute(GuildPermission guildPerms)
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
                if (user == null) currentPerms = DiscordExtensions.CorrectDmPermissions; // They got these wrong in the library
                else currentPerms = (ChannelPermission)user.GetPermissions(context.Channel as IGuildChannel).RawValue;

                if (currentPerms.HasFlag(channelPerms)) return PreconditionResult.FromSuccess();
                else return PreconditionResult.FromError($"{name} requires guild permission {(channelPerms ^ currentPerms) & channelPerms}");
            }
        }
    }


    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    class BetterRequireBotPermissionAttribute : BasePermissionAttribute
    {
        public BetterRequireBotPermissionAttribute(ChannelPermission channelPerms) : base(channelPerms) { }
        public BetterRequireBotPermissionAttribute(GuildPermission guildPerms) : base(guildPerms) { }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            return CheckPermissions(context, command, context.Guild == null ? null : await context.Guild.GetCurrentUserAsync(), "Bot");
        }
    }


    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    class BetterRequireUserPermissionAttribute : BasePermissionAttribute
    {
        public BetterRequireUserPermissionAttribute(ChannelPermission channelPerms) : base(channelPerms) { }
        public BetterRequireUserPermissionAttribute(GuildPermission guildPerms) : base(guildPerms) { }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            return Task.FromResult(CheckPermissions(context, command, context.User as IGuildUser, "User"));
        }
    }





    // https://github.com/Joe4evr/Discord.Addons/blob/master/src/Discord.Addons.Preconditions/Ratelimit/RatelimitAttribute.cs

    /// <summary> Sets how often a user is allowed to use this command
    /// or any command in this module. </summary>
    /// <remarks>This is backed by an in-memory collection
    /// and will not persist with restarts.</remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class RatelimitAttribute : PreconditionAttribute
    {
        private readonly uint invokeLimit;
        private readonly bool noLimitInDMs;
        private readonly bool noLimitForAdmins;
        private readonly bool applyPerGuild;
        private readonly TimeSpan invokeLimitPeriod;
        private readonly Dictionary<(ulong, ulong?), CommandTimeout> invokeTracker = new Dictionary<(ulong, ulong?), CommandTimeout>();

        /// <summary> Sets how often a user is allowed to use this command. </summary>
        /// <param name="times">The number of times a user may use the command within a certain period.</param>
        /// <param name="period">The amount of time since first invoke a user has until the limit is lifted.</param>
        /// <param name="measure">The scale in which the <paramref name="period"/> parameter should be measured.</param>
        /// <param name="flags">Flags to set behavior of the ratelimit.</param>
        public RatelimitAttribute(uint times, double period, Measure measure, RatelimitFlags flags = RatelimitFlags.None)
        {
            invokeLimit = times;
            noLimitInDMs = (flags & RatelimitFlags.NoLimitInDMs) != 0;
            noLimitForAdmins = (flags & RatelimitFlags.NoLimitForAdmins) != 0;
            applyPerGuild = (flags & RatelimitFlags.ApplyPerGuild) != 0;

            switch (measure)
            {
                case Measure.Days:
                    invokeLimitPeriod = TimeSpan.FromDays(period);
                    break;
                case Measure.Hours:
                    invokeLimitPeriod = TimeSpan.FromHours(period);
                    break;
                case Measure.Minutes:
                    invokeLimitPeriod = TimeSpan.FromMinutes(period);
                    break;
            }
        }
        

        /// <inheritdoc />
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (noLimitInDMs && context.Channel is IPrivateChannel)
                return Task.FromResult(PreconditionResult.FromSuccess());

            if (noLimitForAdmins && context.User is IGuildUser gu && gu.GuildPermissions.Administrator)
                return Task.FromResult(PreconditionResult.FromSuccess());

            var now = DateTime.UtcNow;
            var key = applyPerGuild ? (context.User.Id, context.Guild?.Id) : (context.User.Id, null);

            var timeout = (invokeTracker.TryGetValue(key, out var t) && ((now - t.FirstInvoke) < invokeLimitPeriod)) ? t : new CommandTimeout(now);

            timeout.TimesInvoked++;

            if (timeout.TimesInvoked <= invokeLimit)
            {
                invokeTracker[key] = timeout;
                return Task.FromResult(PreconditionResult.FromSuccess());
            }
            else
            {
                return Task.FromResult(PreconditionResult.FromError("You are currently in Timeout."));
            }
        }


        private class CommandTimeout
        {
            public uint TimesInvoked { get; set; }
            public DateTime FirstInvoke { get; }

            public CommandTimeout(DateTime timeStarted)
            {
                FirstInvoke = timeStarted;
            }
        }
    }

    /// <summary> Sets the scale of the period parameter. </summary>
    public enum Measure
    {
        Days,
        Hours,
        Minutes
    }

    /// <summary> Used to set behavior of the ratelimit </summary>
    [Flags]
    public enum RatelimitFlags
    {
        /// <summary> Set none of the flags. </summary>
        None = 0,

        /// <summary> Set whether or not there is no limit to the command in DMs. </summary>
        NoLimitInDMs = 1 << 0,

        /// <summary> Set whether or not there is no limit to the command for guild admins. </summary>
        NoLimitForAdmins = 1 << 1,

        /// <summary> Set whether or not to apply a limit per guild. </summary>
        ApplyPerGuild = 1 << 2
    }
}
