using System.Linq;
using Discord;
using Discord.WebSocket;
using PacManBot.Services;
using static PacManBot.Games.GameUtils;

namespace PacManBot.Games
{
    public abstract class TwoPlayerGame : BaseGame
    {
        public Player turn = Player.Red;
        public Player winner = Player.None;
        public string message = "";

        public bool AITurn => State == State.Active && User(turn).IsBot;
        public bool BotVsBot => User(0).IsBot && User(1).IsBot;

        public IUser User(int i = 0) => i < UserId.Length ? client.GetUser(UserId[i]) : null;
        public IUser User(Player player) => User((int)player);



        protected TwoPlayerGame(ulong channelId, ulong[] userId, DiscordShardedClient client, LoggingService logger, StorageService storage)
            : base(channelId, userId, client, logger, storage) {}


        public abstract void DoTurnAI();


        public override string GetContent(bool showHelp = true)
        {
            if (State != State.Cancelled && UserId[0] != UserId[1] && UserId.Contains(client.CurrentUser.Id))
            {
                if (message == "") message = GlobalRandom.Choose(StartTexts);
                else if (Time > 1 && winner == Player.None && (!BotVsBot || Time % 2 == 0)) message = GlobalRandom.Choose(GameTexts);
                else if (winner != Player.None)
                {
                    if (winner != Player.Tie && UserId[(int)winner] == client.CurrentUser.Id) message = GlobalRandom.Choose(WinTexts);
                    else message = GlobalRandom.Choose(NotWinTexts);
                }

                return message;
            }

            if (State == State.Active)
            {
                if (UserId[0] == UserId[1])
                {
                    return "Feeling lonely, or just testing the bot?";
                }
                if (Time == 0 && showHelp && UserId.Length > 1 && UserId[0] != UserId[1])
                {
                    return $"{User(0).Mention} You were invited to play {Name}.\nChoose an action below, or type **{storage.GetPrefix(Guild)}cancel** if you don't want to play";
                }
            }

            return "";
        }



        protected string StripPrefix(string value)
        {
            return value.Replace(storage.GetPrefix(Guild), "").Trim();
        }


        protected string EmbedTitle()
        {
            return (winner == Player.None) ? $"{turn} Player's turn" :
                winner == Player.Tie ? "It's a tie!" :
                UserId[0] != UserId[1] ? $"{turn} is the winner!" :
                UserId[0] == client.CurrentUser.Id ? "I win!" : "A winner is you!"; // These two are for laughs
        }
    }
}
