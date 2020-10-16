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
        private readonly DiscordShardedClient client;
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
            foreach (var (shard, commands) in await client.GetCommandsNextAsync())
            {
                commands.RegisterCommands(typeof(BaseModule).Assembly);
                commands.SetHelpFormatter<HelpFormatter>();
            }

            await games.LoadGamesAsync();

            client.Ready += OnReadyAsync;
            client.ClientErrored += OnClientErrored;
            client.SocketErrored += OnSocketErrored;
            client.GuildCreated += OnJoinedGuild;
            client.GuildDeleted += OnLeftGuild;
            client.ChannelDeleted += OnChannelDeleted;

            schedule.PrepareRestart += StopAsync;

            await client.StartAsync();
            //await client.UpdateStatusAsync(
            //    new DiscordActivity("Booting up...", ActivityType.Playing), UserStatus.Idle, DateTime.Now);

            if (!string.IsNullOrWhiteSpace(config.discordBotListToken))
            {
                discordBotList = new AuthDiscordBotListApi(client.CurrentUser.Id, config.discordBotListToken);
            }
        }


        /// <summary>Safely stop most activity from the bot and disconnect from Discord.</summary>
        public async Task StopAsync()
        {
            await client.UpdateStatusAsync(userStatus: UserStatus.DoNotDisturb); // why not

            foreach (var shard in client.ShardClients.Values)
                input.StopListening(shard);

            schedule.StopTimers();

            client.Ready -= OnReadyAsync;
            client.ClientErrored -= OnClientErrored;
            client.SocketErrored -= OnSocketErrored;
            client.GuildCreated -= OnJoinedGuild;
            client.GuildDeleted -= OnLeftGuild;
            client.ChannelDeleted -= OnChannelDeleted;

            await Task.Delay(5_000); // Buffer time to finish up doing whatever

            await client.StopAsync();
        }




        private Task OnClientErrored(DiscordClient shard, ClientErrorEventArgs args)
        {
            log.Exception($"On {args.EventName} handler", args.Exception);
            return Task.CompletedTask;
        }


        private Task OnSocketErrored(DiscordClient shard, SocketErrorEventArgs args)
        {
            log.Exception($"On shard {shard.ShardId}", args.Exception);
            return Task.CompletedTask;
        }


        private Task OnReadyAsync(DiscordClient shard, ReadyEventArgs args)
        {
            _ = InnerOnReadyAsync(shard);
            return Task.CompletedTask;
        }


        private async Task InnerOnReadyAsync(DiscordClient shard)
        {
            input.StartListening(shard);

            if (++ready == shard.ShardCount)
            {
                schedule.StartTimers();
                await client.UpdateStatusAsync(
                    new DiscordActivity($"with you!", ActivityType.Playing), UserStatus.Online, DateTime.Now);
                log.Info($"All Shards ready");
            }

            if (File.Exists(Files.ManualRestart))
            {
                try
                {
                    ulong[] id = File.ReadAllText(Files.ManualRestart)
                    .Split("/").Select(ulong.Parse).ToArray();

                    var channel = await shard.GetChannelAsync(id[0]);
                    if (channel != null)
                    {
                        File.Delete(Files.ManualRestart);
                        var message = await channel.GetMessageAsync(id[1]);
                        if (message != null) await message.ModifyAsync(CustomEmoji.Check);
                        log.Info("Resumed after manual restart");
                    }
                }
                catch (Exception e)
                {
                    log.Exception("After manual restart", e);
                    if (File.Exists(Files.ManualRestart) && !(e is IOException)) File.Delete(Files.ManualRestart);
                }
            }
        }


        private async Task OnJoinedGuild(DiscordClient shard, GuildCreateEventArgs args)
        {
            await UpdateGuildCountAsync();
        }


        private async Task OnLeftGuild(DiscordClient shard, GuildDeleteEventArgs args)
        {
            foreach (var channel in args.Guild.Channels)
            {
                games.Remove(games.GetForChannel(channel.Key));
            }

            await UpdateGuildCountAsync();
        }


        private Task OnChannelDeleted(DiscordClient shard, ChannelDeleteEventArgs args)
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
            catch (Exception e)
            {
                log.Exception("While updating guild count", e);
            }
        }
    }
}
