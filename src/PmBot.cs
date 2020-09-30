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

namespace PacManBot
{
    /// <summary>
    /// Runs an instance of Pac-Man Bot and handles its connection to Discord.
    /// </summary>
    public class PmBot
    {
        /// <summary>Runtime configuration of the bot.</summary>
        public PmConfig Config { get; }

        private readonly DiscordShardedClient client;
        private readonly LoggingService log;
        private readonly StorageService storage;
        private readonly GameService games;
        private readonly InputService input;
        private readonly PmCommandService commands;
        private readonly SchedulingService schedule;

        private AuthDiscordBotListApi discordBotList = null;
        private DateTime lastGuildCountUpdate = DateTime.MinValue;


        public PmBot(PmConfig config, DiscordShardedClient client, LoggingService log, StorageService storage,
            GameService games, InputService input, PmCommandService commands, SchedulingService schedule)
        {
            Config = config;
            this.client = client;
            this.log = log;
            this.storage = storage;
            this.games = games;
            this.input = input;
            this.commands = commands;
            this.schedule = schedule;
        }


        /// <summary>Starts the bot and its connection to Discord.</summary>
        public async Task StartAsync()
        {
            await commands.AddAllModulesAsync();
            await games.LoadGamesAsync();

            client.Ready += ReadyAsync;
            schedule.PrepareRestart += StopAsync;

            await client.StartAsync();
            await client.UpdateStatusAsync(
                new DiscordActivity("Booting up...", ActivityType.Custom), UserStatus.Idle, DateTime.Now);

            if (!string.IsNullOrWhiteSpace(Config.discordBotListToken))
            {
                discordBotList = new AuthDiscordBotListApi(client.CurrentUser.Id, Config.discordBotListToken);
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
