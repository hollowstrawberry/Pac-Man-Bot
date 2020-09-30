using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DiscordBotsList.Api;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Services;
using DSharpPlus.EventArgs;
using DSharpPlus.Entities;
using DSharpPlus.CommandsNext;

namespace PacManBot
{
    /// <summary>
    /// Runs an instance of Pac-Man Bot and handles its connection to Discord.
    /// </summary>
    public class PmBot
    {
        private readonly PmBotConfig config;
        private readonly IServiceProvider services;
        private readonly DiscordShardedClient client;
        private readonly LoggingService log;
        private readonly GameService games;
        private readonly InputService input;
        private readonly SchedulingService schedule;

        private AuthDiscordBotListApi discordBotList = null;
        private DateTime lastGuildCountUpdate = DateTime.MinValue;


        public PmBot(PmBotConfig config, IServiceProvider services)
        {
            this.config = config;
            this.services = services;
            client = services.Get<DiscordShardedClient>();
            log = services.Get<LoggingService>();
            games = services.Get<GameService>();
            input = services.Get<InputService>();
            schedule = services.Get<SchedulingService>();
        }


        /// <summary>Starts the bot and its connection to Discord.</summary>
        public async Task StartAsync()
        {
            await client.UseCommandsNextAsync(new CommandsNextConfiguration
            {
                UseDefaultCommandHandler = false,
                Services = services,
            });
            foreach (var (shard, commands) in client.GetCommandsNext())
            {
                commands.RegisterCommands(typeof(PmBot).Assembly);
                commands.CommandExecuted += OnCommandExecuted;
                commands.CommandErrored += OnCommandErrored;
            }

            await games.LoadGamesAsync();

            client.Ready += ReadyAsync;
            schedule.PrepareRestart += StopAsync;

            await client.StartAsync();
            await client.UpdateStatusAsync(
                new DiscordActivity("Booting up...", ActivityType.Custom), UserStatus.Idle, DateTime.Now);

            if (!string.IsNullOrWhiteSpace(config.discordBotListToken))
            {
                discordBotList = new AuthDiscordBotListApi(client.CurrentUser.Id, config.discordBotListToken);
            }
        }


        private async Task ReadyAsync(ReadyEventArgs args)
        {
            var shard = args.Client;
            log.Info($"Shard {shard.ShardId} is ready");

            input.StartListening(shard);
            shard.GuildCreated += OnJoinedGuild;
            shard.GuildDeleted += OnLeftGuild;
            shard.ChannelDeleted += OnChannelDeleted;

            await shard.UpdateStatusAsync(
                new DiscordActivity($"with you!", ActivityType.Playing), UserStatus.Online, DateTime.Now);

            if (schedule.timers.Count == 0) schedule.StartTimers();

            if (File.Exists(Files.ManualRestart))
            {
                try
                {
                    ulong[] id = File.ReadAllText(Files.ManualRestart)
                        .Split("/").Select(ulong.Parse).ToArray();

                    var channel = await shard.GetChannelAsync(id[0]);
                    if (channel != null)
                    {
                        var message = await channel.GetMessageAsync(id[1]);
                        if (message != null) await message.ModifyAsync(CustomEmoji.Check);
                        File.Delete(Files.ManualRestart);
                        log.Info("Resumed after manual restart");
                    }
                }
                catch (Exception e)
                {
                    log.Warning($"Resuming after manual restart: {e.Message}");
                }
            }
        }


        /// <summary>Safely stop most activity from the bot and disconnect from Discord.</summary>
        public async Task StopAsync()
        {
            await client.UpdateStatusAsync(userStatus: UserStatus.DoNotDisturb); // why not

            foreach (var shard in client.ShardClients.Values) input.StopListening(shard);
            schedule.StopTimers();
            client.GuildCreated -= OnJoinedGuild;
            client.GuildDeleted -= OnLeftGuild;
            client.ChannelDeleted -= OnChannelDeleted;

            await Task.Delay(6_000); // Buffer time to finish up doing whatever

            await client.StopAsync();
        }


        private Task OnCommandExecuted(CommandExecutionEventArgs args)
        {
            log.Verbose($"Executed {args.Command.Name} for {args.Context.User.DebugName()} in {args.Context.Channel.DebugName()}");
            return Task.CompletedTask;
        }

        private async Task OnCommandErrored(CommandErrorEventArgs args)
        {
            if (args.Exception.InnerException != null)
            {
                await args.Context.RespondAsync($"Something went wrong! {args.Exception.InnerException.Message}");
                log.Exception($"While executing {args.Command.Name} for {args.Context.User.DebugName()} " +
                    $"in {args.Context.Channel.DebugName()}", args.Exception);
            }
            else
            {
                await args.Context.RespondAsync(args.Exception.Message);
                log.Verbose($"Couldn't execute {args.Command.Name} for {args.Context.User.DebugName()} " +
                    $"in {args.Context.Channel.DebugName()}", args.Exception.Message);
            }
        }


        private async Task OnJoinedGuild(GuildCreateEventArgs args)
        {
            await UpdateGuildCountAsync();
        }

        private async Task OnLeftGuild(GuildDeleteEventArgs args)
        {
            foreach (var channel in args.Guild.Channels)
            {
                games.Remove(games.GetForChannel(channel.Key));
            }

            await UpdateGuildCountAsync();
        }


        private Task OnChannelDeleted(ChannelDeleteEventArgs args)
        {
            games.Remove(games.GetForChannel(args.Channel.Id));

            return Task.CompletedTask;
        }


        private async Task UpdateGuildCountAsync()
        {
            if (discordBotList == null || (DateTime.Now - lastGuildCountUpdate).TotalMinutes < 30.0) return;

            int guilds = 0;
            foreach (var shard in client.ShardClients.Values)
            {
                guilds += shard.Guilds.Count;
            }

            var recordedGuilds = (await discordBotList.GetBotStatsAsync(client.CurrentUser.Id)).GuildCount;
            if (recordedGuilds < guilds)
            {
                await discordBotList.UpdateStats(guilds, client.ShardClients.Count);
                lastGuildCountUpdate = DateTime.Now;
            }

            log.Info($"Guild count updated to {guilds}");
        }
    }
}
