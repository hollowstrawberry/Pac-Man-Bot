using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
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
        private readonly LoggingService log;
        private readonly StorageService storage;

        private IReadOnlyDictionary<string, CommandHelp> commandHelp;
        private IReadOnlyDictionary<string, IEnumerable<CommandHelp>> moduleHelp;


        public PmCommandService(IServiceProvider services, PmConfig config,
            PmDiscordClient client, LoggingService log, StorageService storage)
            : base(CommandConfig)
        {
            this.services = services;
            this.client = client;
            this.log = log;
            this.storage = storage;

            CommandExecuted += OnCommandExecuted;
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
                    tempCommandHelp[alias.ToLowerInvariant()] = help;
                }
            }
            commandHelp = tempCommandHelp;

            moduleHelp = allCommands
                .GroupBy(c => c.Module)
                .OrderBy(g => g.Key.Remarks)
                .ToDictionary(
                    g => g.Key.Name,
                    g => g.Select(c => commandHelp[c.Name.ToLowerInvariant()])
                        .ToArray().AsEnumerable());

            log.Info($"Added {allCommands.Length} commands", LogSource.Command);
        }


        /// <summary>Finds and executes a command.</summary>
        public async Task ExecuteAsync(SocketUserMessage message, int commandPos)
        {
            var context = new PmCommandContext(message, commandPos, services);
            await ExecuteAsync(context, commandPos, services, MultiMatchHandling.Best);
        }


        private async Task OnCommandExecuted(Optional<CommandInfo> command, ICommandContext genericContext, IResult result)
        {
            if (!(genericContext is PmCommandContext context))
            {
                log.Error($"On command executed: Expected PmCommandContext, got {genericContext.GetType()}");
                return;
            }

            var errorReply = CommandErrorReply(result, context);
            if (errorReply != null) await context.Channel.SendMessageAsync(errorReply, options: PmBot.DefaultOptions);


            if (!command.IsSpecified) return;

            string user = context.User.FullName();
            string channel = context.Channel.FullName();
            string commandText = context.Message.Content.Substring(context.Position);
            if (commandText.Length > 40) commandText = commandText.Truncate(37) + "...";

            if (result.IsSuccess)
            {
                log.Verbose($"Executed \"{commandText}\" for {user} in {channel}", LogSource.Command);
            }
            else if (result is ExecuteResult execResult && execResult.Exception != null)
            {
                log.Exception($"Executing \"{commandText}\" for {user} in {channel}", execResult.Exception, LogSource.Command);
            }
            else
            {
                log.Verbose($"Couldn't execute \"{commandText}\" for {user} in {channel}: {result.ErrorReason}", LogSource.Command);
            }
        }




        /// <summary>Gets a message embed of the user manual for a command.</summary>
        public EmbedBuilder GetCommandHelp(string commandName, string prefix = "")
        {
            if (!commandHelp.TryGetValue(commandName.ToLowerInvariant(), out var help)) return null;
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




        private string CommandErrorReply(IResult result, PmCommandContext context)
        {
            var error = result.ErrorReason;
            var type = result.Error ?? CommandError.Unsuccessful;

            if (type == CommandError.Exception)
            {
                return null;
            }
            if (type == CommandError.UnknownCommand)
            {
                if (context.Guild == null)
                    return $"Unknown command! Send `{context.Prefix}help` for a list.";

                return null;
            }
            if (type == CommandError.ParseFailed)
            {
                if (error.ContainsAny("quoted parameter", "one character of whitespace"))
                    return "Incorrect use of quotes in command parameters.";

                return $"Invalid command parameters! Please use `{context.Prefix}help [command name]` or try again.";
            }
            if (type == CommandError.BadArgCount)
            {
                if (error.Contains("few"))
                    return $"Missing command parameters! Please use `{context.Prefix}help [command name]` or try again.";
                if (error.Contains("many"))
                    return $"Too many parameters! Please use `{context.Prefix}help [command name]` or try again.";
            }
            if (type == CommandError.ObjectNotFound)
            {
                if (error.StartsWith("User"))
                    return "Can't find the specified user!";
                if (error.StartsWith("Channel"))
                    return "Can't find the specified channel!";
            }
            if (type == CommandError.UnmetPrecondition)
            {
                if (error.Contains("owner"))
                    return null;
                if (context.Guild == null)
                    return "You need to be in a guild to use this command!";
                if (error.StartsWith("Bot"))
                    return $"This bot is missing the permission**{Regex.Replace(error.Split(' ').Last(), @"([A-Z])", @" $1")}**!";
                if (error.StartsWith("User"))
                    return $"You need the permission**{Regex.Replace(error.Split(' ').Last(), @"([A-Z])", @" $1")}** to use this command!";
                if (error.StartsWith("Invalid context"))
                    return "This command can only be used in DMs with the bot, or if you have the right permissions.";
            }

            return error;
        }




        private class CommandEqComp : IEqualityComparer<CommandInfo>
        {
            public static readonly CommandEqComp Instance = new CommandEqComp();

            public bool Equals(CommandInfo x, CommandInfo y) => x.Name == y.Name;
            public int GetHashCode(CommandInfo obj) => obj.Name.GetHashCode();
        }
    }
}
