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
using PacManBot.Commands;

namespace PacManBot
{
    /// <summary>
    /// Runs an instance of Pac-Man Bot and handles its connection to Discord.
    /// </summary>
    public class PmBot
    {
        private readonly PmBotConfig config;
        private readonly IServiceProvider services;
        private readonly DiscordShardedClient shardedClient;
        private readonly LoggingService log;
        private readonly GameService games;
        private readonly InputService input;
        private readonly SchedulingService schedule;

        private AuthDiscordBotListApi discordBotList = null;
        private DateTime lastGuildCountUpdate = DateTime.MinValue;
        private int ready = 0;


        public PmBot(PmBotConfig config, IServiceProvider services)
        {
            this.config = config;
            this.services = services;
            shardedClient = services.Get<DiscordShardedClient>();
            log = services.Get<LoggingService>();
            games = services.Get<GameService>();
            input = services.Get<InputService>();
            schedule = services.Get<SchedulingService>();
        }


        /// <summary>Starts the bot and its connection to Discord.</summary>
        public async Task StartAsync()
        {
            await shardedClient.UseCommandsNextAsync(new CommandsNextConfiguration
            {
                UseDefaultCommandHandler = false,
                Services = services,
            });
            foreach (var (shard, commands) in await shardedClient.GetCommandsNextAsync())
            {
                commands.RegisterCommands(typeof(BaseModule).Assembly);
                commands.SetHelpFormatter<HelpFormatter>();
            }

            await games.LoadGamesAsync();

            shardedClient.Ready += OnReadyAsync;
            shardedClient.ClientErrored += OnClientErrored;
            schedule.PrepareRestart += StopAsync;

            await shardedClient.StartAsync();
            //await client.UpdateStatusAsync(
            //    new DiscordActivity("Booting up...", ActivityType.Playing), UserStatus.Idle, DateTime.Now);

            if (!string.IsNullOrWhiteSpace(config.discordBotListToken))
            {
                discordBotList = new AuthDiscordBotListApi(shardedClient.CurrentUser.Id, config.discordBotListToken);
            }
        }


        /// <summary>Safely stop most activity from the bot and disconnect from Discord.</summary>
        public async Task StopAsync()
        {
            await shardedClient.UpdateStatusAsync(userStatus: UserStatus.DoNotDisturb); // why not

            foreach (var shard in shardedClient.ShardClients.Values) input.StopListening(shard);
            schedule.StopTimers();
            shardedClient.GuildCreated -= OnJoinedGuild;
            shardedClient.GuildDeleted -= OnLeftGuild;
            shardedClient.ChannelDeleted -= OnChannelDeleted;

            await Task.Delay(6_000); // Buffer time to finish up doing whatever

            await shardedClient.StopAsync();
        }




        private Task OnClientErrored(DiscordClient sender, ClientErrorEventArgs e)
        {
            log.Exception($"On {e.EventName} handler", e.Exception);
            return Task.CompletedTask;
        }


        private Task OnReadyAsync(DiscordClient client, ReadyEventArgs args)
        {
            if (++ready == client.ShardCount)
            {
                _ = OnAllReadyAsync();
            }
            return Task.CompletedTask;
        }


        private async Task OnAllReadyAsync()
        {
            foreach (var shard in shardedClient.ShardClients.Values)
            {
                input.StartListening(shard);
                shard.GuildCreated += OnJoinedGuild;
                shard.GuildDeleted += OnLeftGuild;
                shard.ChannelDeleted += OnChannelDeleted;
                await Task.Delay(5000); // give it time to process events
                await shard.UpdateStatusAsync(
                    new DiscordActivity($"with you!", ActivityType.Playing), UserStatus.Online, DateTime.Now);
            }

            log.Info($"All Shards ready");

            schedule.StartTimers();

            if (File.Exists(Files.ManualRestart))
            {
                try
                {
                    ulong[] id = File.ReadAllText(Files.ManualRestart)
                        .Split("/").Select(ulong.Parse).ToArray();

                    foreach (var shard in shardedClient.ShardClients.Values)
                    {
                        var channel = await shard.GetChannelAsync(id[0]);
                        if (channel != null)
                        {
                            var message = await channel.GetMessageAsync(id[1]);
                            if (message != null) await message.ModifyAsync(CustomEmoji.Check);
                            File.Delete(Files.ManualRestart);
                            log.Info("Resumed after manual restart");
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    log.Warning($"Resuming after manual restart: {e.Message}");
                }
            }
        }


        private async Task OnJoinedGuild(DiscordClient sender, GuildCreateEventArgs args)
        {
            await UpdateGuildCountAsync();
        }


        private async Task OnLeftGuild(DiscordClient sender, GuildDeleteEventArgs args)
        {
            foreach (var channel in args.Guild.Channels)
            {
                games.Remove(games.GetForChannel(channel.Key));
            }

            await UpdateGuildCountAsync();
        }


        private Task OnChannelDeleted(DiscordClient sender, ChannelDeleteEventArgs args)
        {
            games.Remove(games.GetForChannel(args.Channel.Id));

            return Task.CompletedTask;
        }


        private async Task UpdateGuildCountAsync()
        {
            try
            {
                if (discordBotList == null || (DateTime.Now - lastGuildCountUpdate).TotalMinutes < 30.0) return;

                int guilds = 0;
                foreach (var shard in shardedClient.ShardClients.Values)
                {
                    guilds += shard.Guilds.Count;
                }

                var recordedGuilds = (await discordBotList.GetBotStatsAsync(shardedClient.CurrentUser.Id)).GuildCount;
                if (recordedGuilds < guilds)
                {
                    await discordBotList.UpdateStats(guilds, shardedClient.ShardClients.Count);
                    lastGuildCountUpdate = DateTime.Now;
                }

                log.Info($"Guild count updated to {guilds}");
            }
            catch (Exception e)
            {
                log.Exception("While updating guild count", e);
            }
        }
    }
}
