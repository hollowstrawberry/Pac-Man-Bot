using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using Discord;
using Discord.WebSocket;
using PacManBot.Constants;
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
        protected CancellationTokenSource discordRequestCTS = new CancellationTokenSource();

        public Player turn = Player.Red;
        public Player winner = Player.None; // For two-player games
        [DataMember] public State state = State.Active; // For one-player games
        [DataMember] public DateTime lastPlayed;
        [DataMember] public int time = 0; //How many turns have passed
        [DataMember] public readonly ulong channelId; //Which channel this game is located in
        [DataMember] public ulong messageId = 1; //The focus message of the game. Even if not set, it must be a number above 0 or else a call to get the message object will throw an error
        [DataMember] public ulong[] userId; //Users playing this game

        public abstract string Name { get; }
        public abstract TimeSpan Expiry { get; }
        public virtual string GameFile => $"{Folder}{Name}{channelId}{Extension}";
        public virtual RequestOptions RequestOptions => new RequestOptions() { Timeout = 10000, RetryMode = RetryMode.RetryRatelimit, CancelToken = discordRequestCTS.Token };

        public ISocketMessageChannel Channel => client.GetChannel(channelId) as ISocketMessageChannel;
        public SocketGuild Guild => (client.GetChannel(channelId) as SocketGuildChannel)?.Guild;
        public async Task<IUserMessage> GetMessage() => (await Channel.GetMessageAsync(messageId, options: Utils.DefaultRequestOptions)) as IUserMessage;
        public IUser User(Player player) => User((int)player);
        public IUser User(int i = 0) => i < userId.Length ? client.GetUser(userId[i]) : null;


        protected GameInstance() { } // Used when deserializing

        protected GameInstance(ulong channelId, ulong[] userId, DiscordShardedClient client, LoggingService logger, StorageService storage)
        {
            this.client = client;
            this.logger = logger;
            this.storage = storage;
            this.channelId = channelId;
            this.userId = userId;

            lastPlayed = DateTime.Now;
        }



        public abstract bool IsInput(string value);


        public virtual void DoTurn(string input)
        {
            if (state != State.Active || winner != Player.None) return; //Failsafe
            lastPlayed = DateTime.Now;
        }


        public virtual string GetContent(bool showHelp = true)
        {
            if (state != State.Cancelled && userId.Contains(client.CurrentUser.Id))
            {
                if (time < userId.Length) return GlobalRandom.Choose(StartTexts);
                if (time < userId.Length) return GlobalRandom.Choose(StartTexts);

                if (winner == Player.None) return GlobalRandom.Choose(GameTexts);
                else if (userId[(int)winner] == client.CurrentUser.Id) return GlobalRandom.Choose(WinTexts);
                else return GlobalRandom.Choose(NotWinTexts);
            }

            if (state == State.Active)
            {
                if (userId[0] == userId[1])
                {
                    return "Feeling lonely, or just testing the bot?";
                }
                if (time == 0 && showHelp && userId.Length > 1 && userId[0] != userId[1])
                {
                    return $"{User(0).Mention} You were invited to play {Name}. If you don't want to play, type **{storage.GetPrefix(Guild)}cancel**";
                }
            }

            return "";
        }


        public virtual EmbedBuilder GetEmbed(bool showHelp = true)
        {
            return null;
        }


        public virtual void SetServices(DiscordShardedClient client, LoggingService logger, StorageService storage)
        {
            this.client = client;
            this.logger = logger;
            this.storage = storage;
        }


        public virtual void CancelRequests()
        {
            discordRequestCTS.Cancel();
            discordRequestCTS = new CancellationTokenSource();
        }


        protected string StripPrefix(string value)
        {
            return value.Replace(storage.GetPrefix(Guild), "").Trim();
        }


        protected string EmbedTitle()
        {
            return (winner == Player.None) ? $"{turn} Player's turn" :
                winner == Player.Tie ? "It's a tie!" :
                userId[0] != userId[1] ? $"{turn} is the winner!" :
                userId[0] == client.CurrentUser.Id ? "I win!" : "A winner is you!"; // These two are for laughs
        }


        protected EmbedBuilder CancelledEmbed()
        {
            return new EmbedBuilder()
            {
                Title = Name,
                Description = DateTime.Now - lastPlayed > Expiry ? "Game timed out" : "Game cancelled",
                Color = Player.None.Color(),
            };
        }
    }
}
