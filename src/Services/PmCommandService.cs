using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Commands;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Utils;

namespace PacManBot.Services
{
    /// <summary>
    /// Provides access to command information and execution.
    /// </summary>
    public class PmCommandService : CommandService
    {
        private static readonly CommandServiceConfig CommandConfig = new CommandServiceConfig
        {
            DefaultRunMode = RunMode.Async,
            CaseSensitiveCommands = false,
            LogLevel = 0,
        };

        private readonly IServiceProvider services;
        private readonly PmDiscordClient client;
        private readonly LoggingService logger;
        private readonly StorageService storage;

        private IReadOnlyDictionary<string, CommandHelp> commandHelp;
        private IReadOnlyDictionary<string, IEnumerable<CommandHelp>> moduleHelp;


        public PmCommandService(IServiceProvider services, PmConfig config,
            PmDiscordClient client, LoggingService logger, StorageService storage)
            : base(CommandConfig)
        {
            this.services = services;
            this.client = client;
            this.logger = logger;
            this.storage = storage;

            CommandExecuted += LogCommand;
        }


        /// <summary>Adds all command modules in this assembly.</summary>
        public async Task AddAllModulesAsync()
        {
            await AddModulesAsync(typeof(PmBaseModule).Assembly, services);

            var allCommands = Commands
                .OrderByDescending(c => c.Priority)
                .Distinct(CommandEqComp.Instance)
                .ToArray();

            var tempCommandHelp = new Dictionary<string, CommandHelp>();
            foreach (var com in allCommands)
            {
                var help = new CommandHelp(com);
                foreach (var alias in com.Aliases)
                {
                    tempCommandHelp[alias.ToLower()] = help;
                }
            }
            commandHelp = tempCommandHelp;

            moduleHelp = allCommands
                .GroupBy(c => c.Module)
                .OrderBy(g => g.Key.Remarks)
                .ToDictionary(
                    g => g.Key.Name,
                    g => g.Select(c => commandHelp[c.Name.ToLower()])
                        .ToArray().AsEnumerable());

            logger.Log(LogSeverity.Info, LogSource.Command, $"Added {allCommands.Length} commands");
        }


        private async Task LogCommand(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (!command.IsSpecified) return;

            if (result.IsSuccess)
            {
                await logger.Log(
                    LogSeverity.Verbose, LogSource.Command,
                    $"Executed \"{context.Message.Content}\" for {context.User.FullName()} in {context.Channel.FullName()}");
            }
            else if (result is ExecuteResult execResult && execResult.Exception != null)
            {
                await logger.Log(
                    LogSeverity.Error, LogSource.Command,
                    $"Executing \"{context.Message.Content}\" for {context.User.FullName()} " +
                    $"in {context.Channel.FullName()}: {execResult.Exception}");
            }
        }
        



        /// <summary>Attempts to find and execute a command.</summary>
        public async Task<ExecuteResult> TryExecuteAsync(SocketUserMessage message)
        {
            string prefix = await storage.GetGuildPrefixAsync((message.Channel as SocketGuildChannel)?.Guild);
            int commandPosition = 0;

            if (message.HasMentionPrefix(client.CurrentUser, ref commandPosition)
                || message.HasStringPrefix($"{prefix} ", ref commandPosition)
                || message.HasStringPrefix(prefix, ref commandPosition)
                || !await storage.RequiresPrefixAsync(message.Channel))
            {
                var context = new PmCommandContext(message, services);
                var res = await ExecuteAsync(context, commandPosition, services, MultiMatchHandling.Best);

                if (res.IsSuccess) return ExecuteResult.FromSuccess();
                else
                {
                    var error = res.Error ?? CommandError.Unsuccessful;
                    var reason = CommandErrorReason(res.ErrorReason, context, prefix);
                    return ExecuteResult.FromError(error, reason);
                }
            }
            else
            {
                return ExecuteResult.FromError(CommandError.UnknownCommand, "Unknown command.");
            }
        }


        /// <summary>Gets a message embed of the user manual for a command.</summary>
        public EmbedBuilder GetCommandHelp(string commandName, string prefix = "")
        {
            if (!commandHelp.TryGetValue(commandName.ToLower(), out var help)) return null;
            return help.GetEmbed(prefix);
        }


        /// <summary>Gets a message embed of the user manual about all commands.</summary>
        public async Task<EmbedBuilder> GetAllHelp(ICommandContext context, bool expanded)
        {
            string prefix = storage.GetPrefix(context);

            var embed = new EmbedBuilder()
            {
                Title = $"{CustomEmoji.PacMan} __**Bot Commands**__",

                Description = (prefix == "" ? "No prefix is needed in this channel!" : $"Prefix for this server is '{prefix}'")
                            + $"\nYou can do **{prefix}help command** for more information about a command.\n\n"
                            + $"Parameters: [optional] <needed>".If(expanded),

                Color = CommandHelp.EmbedColor,
            };

            foreach (var module in moduleHelp)
            {
                var moduleText = new StringBuilder();

                foreach (var command in module.Value)
                {
                    if (!command.Hidden)
                    {
                        var conditions = await command.Command.CheckPreconditionsAsync(context, services);
                        if (!conditions.IsSuccess) continue;

                        if (expanded)
                        {
                            moduleText.Append($"**{command.Command.Name} {command.Parameters}**");
                            if (command.Remarks != "") moduleText.Append($" — *{command.Remarks}*");
                            moduleText.Append("\n");
                        }
                        else
                        {
                            moduleText.Append($"**{command.Command.Name}**, ");
                        }
                    }
                }

                if (!expanded && module.Key.Contains("Pac-Man"))
                {
                    moduleText.Append("**bump**, **cancel**"); // This is hardcoded for completeness
                }

                if (moduleText.Length > 0)
                {
                    embed.AddField(module.Key, moduleText.ToString().Trim(' ', ',', '\n'));
                }
            }

            return embed;
        }


        private string CommandErrorReason(string baseError, ICommandContext context, string prefix)
        {
            if (baseError.Contains("requires") && context.Guild == null)
                return "You need to be in a guild to use this command!";

            if (baseError.Contains("Bot requires"))
                return $"This bot is missing the permission**{Regex.Replace(baseError.Split(' ').Last(), @"([A-Z])", @" $1")}**!";

            if (baseError.Contains("User requires"))
                return $"You need the permission**{Regex.Replace(baseError.Split(' ').Last(), @"([A-Z])", @" $1")}** to use this command!";

            if (baseError.Contains("User not found"))
                return "Can't find the specified user!";

            if (baseError.Contains("Failed to parse"))
                return $"Invalid command parameters! Please use `{prefix}help [command name]` or try again.";

            if (baseError.Contains("too few parameters"))
                return $"Missing command parameters! Please use `{prefix}help [command name]` or try again.";

            if (baseError.Contains("too many parameters"))
                return $"Too many parameters! Please use `{prefix}help [command name]` or try again.";

            if (baseError.Contains("must be used in a guild"))
                return "You need to be in a guild to use this command!";

            if (baseError.ContainsAny("quoted parameter", "one character of whitespace"))
                return "Incorrect use of quotes in command parameters.";

            if (baseError.Contains("Timeout"))
                return "You're using that command too much. Please try again later.";

            if (baseError.Contains("must be an owner"))
                return null;

            return baseError;
        }




        private class CommandEqComp : IEqualityComparer<CommandInfo>
        {
            public static readonly CommandEqComp Instance = new CommandEqComp();

            public bool Equals(CommandInfo x, CommandInfo y) => x.Name == y.Name;
            public int GetHashCode(CommandInfo obj) => obj.Name.GetHashCode();
        }
    }
}
