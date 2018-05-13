using System;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Discord;
using Discord.WebSocket;
using PacManBot.Services;
using static PacManBot.Games.GameUtils;

namespace PacManBot.Games
{
    [DataContract] // For JSON serialization
    public abstract class GameInstance
    {
        public const string Folder = "games/";
        public const string Extension = ".json";


        protected DiscordShardedClient client;
        protected StorageService storage;
        protected LoggingService logger;
        protected CancellationTokenSource messageEditCTS = new CancellationTokenSource();

        public Player turn = Player.Red;
        public Player winner = Player.None; // For two-player games
        [DataMember] public State state = State.Active; // For one-player games
        [DataMember] public DateTime lastPlayed;
        [DataMember] public int time = 0; //How many turns have passed
        [DataMember] public readonly ulong channelId; //Which channel this game is located in
        [DataMember] public ulong messageId = 1; //The focus message of the game. Even if not set, it must be a number above 0 or else a call to get the message object will throw an error
        [DataMember] public ulong[] userId; //Users playing this game

        public abstract TimeSpan Expiry { get; }
        public abstract Dictionary<string, GameInput> GameInputs { get; }
        public virtual string GameFile => $"{Folder}{this.GetType().Name}{channelId}{Extension}";
        public virtual RequestOptions MessageEditOptions => new RequestOptions() { Timeout = 10000, RetryMode = RetryMode.RetryRatelimit, CancelToken = messageEditCTS.Token };

        public IMessageChannel Channel => client.GetChannel(channelId) as IMessageChannel;
        public SocketGuild Guild => (client.GetChannel(channelId) as SocketGuildChannel)?.Guild;


        protected GameInstance() { } // Used when deserializing

        protected GameInstance(ulong channelId, ulong[] userId, DiscordShardedClient client, StorageService storage, LoggingService logger)
        {
            this.client = client;
            this.storage = storage;
            this.logger = logger;
            this.channelId = channelId;
            this.userId = userId;

            lastPlayed = DateTime.Now;
        }



        public virtual string GetContent(bool showHelp = true) => "";


        public virtual EmbedBuilder GetEmbed(bool showHelp = true) => null;


        public virtual void DoTurn(GameInput input)
        {
            if (state != State.Active) return; //Failsafe
            lastPlayed = DateTime.Now;
        }

        public virtual void SetServices(DiscordShardedClient client, StorageService storage, LoggingService logger)
        {
            this.client = client;
            this.storage = storage;
            this.logger = logger;
        }

        public virtual void CancelPreviousEdits()
        {
            messageEditCTS.Cancel();
            messageEditCTS = new CancellationTokenSource();
        }
    }
}
