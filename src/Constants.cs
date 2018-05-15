using Discord;

namespace PacManBot.Constants
{
    public static class BotFile
    {
        public const string
            Config = "config.bot",
            Prefixes = "prefixes.bot",
            Scoreboard = "scoreboard.bot",
            Contents = "contents.bot",
            WakaExclude = "wakaexclude.bot",
            FeedbackLog = "logs/feedback.txt",
            CustomMapLog = "logs/custom.txt";
    }


    public static class LogSource
    {
        public const string
            Bot = "Bot",
            Game = "Game",
            Storage = "Storage",
            Scheduling = "Scheduling",
            Owner = "Owner";
    }


    public static class CustomEmoji
    {
        public static readonly Emote
            PacMan = "<a:pacman:409803570544902144>".ToEmote(),
            Help = "<:help:438481218674229248>".ToEmote(),
            Wait = "<:wait:438480194991554561>".ToEmote(),
            Discord = "<:discord:409811304103149569>".ToEmote(),
            GitHub = "<:github:409803419717599234>".ToEmote(),
            Staff = "<:staff:412019879772815361>".ToEmote(),
            Check = "<:check:410612082929565696>".ToEmote(),
            Cross = "<:cross:410612082988285952>".ToEmote(),
            Loading = "<a:loading:410612084527595520>".ToEmote(),
            Dance = "<a:danceblobfast:439575722567401479>".ToEmote(),
            Thinkxel = "<:thinkxel:409803420308996106>".ToEmote(),
            Empty = "<:Empty:445680384592576514>".ToEmote(),

            TTTx = "<:TTTx:445729766952402944>".ToEmote(),
            TTTxHL = "<:TTTxHL:445729766881099776>".ToEmote(),
            TTTo = "<:TTTo:445729766780436490>".ToEmote(),
            TTToHL = "<:TTToHL:445729767371702292>".ToEmote(),
            C4red = "<:C4red:445683639137599492>".ToEmote(),
            C4redHL = "<:C4redHL:445729766327451680>".ToEmote(),
            C4blue = "<:C4blue:445683639817207813>".ToEmote(),
            C4blueHL = "<:C4blueHL:445729766541099011>".ToEmote();

        public static readonly Emote[] NumberCircle = new Emote[]
        {
            "<:0circle:445021371127562280>".ToEmote(),
            "<:1circle:445021372356231186>".ToEmote(),
            "<:2circle:445021372889038858>".ToEmote(),
            "<:3circle:445021372213886987>".ToEmote(),
            "<:4circle:445021372905947138>".ToEmote(),
            "<:5circle:445021373136502785>".ToEmote(),
            "<:6circle:445021373405069322>".ToEmote(),
            "<:7circle:445021372687581185>".ToEmote(),
            "<:8circle:445021372465283098>".ToEmote(),
            "<:9circle:445021373245554699>".ToEmote(),
        };
    }
}
