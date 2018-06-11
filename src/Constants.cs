using Discord;
using PacManBot.Utils;

namespace PacManBot
{
    public enum TimePeriod
    {
        all = -1,
        month = 24 * 30,
        week = 24 * 7,
        day = 24,
        a = all, m = month, w = week, d = day //To be parsed from a string
    }



    public static class BotFile
    {
        public const string
            Config = "config.bot",
            Prefixes = "prefixes.bot",
            Scoreboard = "scoreboard.bot",
            Contents = "contents.bot",
            WakaExclude = "wakaexclude.bot",
            FeedbackLog = "logs/feedback.txt",
            CustomMapLog = "logs/custom.txt",
            GameFolder = "games/",
            GameExtension = ".json";
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
            ECheck = Check.ToEmote(),
            ECross = Cross.ToEmote(),
            ELoading = Loading.ToEmote(),
            EHelp = Help.ToEmote(),
            ERapidBlobDance = RapidBlobDance.ToEmote();


        public const string
            Check = "<:check:410612082929565696>",
            Cross = "<:cross:410612082988285952>",
            Loading = "<a:loading:410612084527595520>",
            PacMan = "<a:pacman:409803570544902144>",
            Help = "<:help:438481218674229248>",
            RapidBlobDance = "<a:danceblobfast:439575722567401479>",

            Discord = "<:discord:409811304103149569>",
            GitHub = "<:github:409803419717599234>",
            Staff = "<:staff:412019879772815361>",
            Thinkxel = "<:thinkxel:409803420308996106>",
            Empty = "<:Empty:445680384592576514>",

            TTTx = "<:TTTx:445729766952402944>",
            TTTxHL = "<:TTTxHL:445729766881099776>",
            TTTo = "<:TTTo:445729766780436490>",
            TTToHL = "<:TTToHL:445729767371702292>",
            C4red = "<:C4red:445683639137599492>",
            C4redHL = "<:C4redHL:445729766327451680>",
            C4blue = "<:C4blue:445683639817207813>",
            C4blueHL = "<:C4blueHL:445729766541099011>",
            BlackCircle = "<:black:451507461556404224>",
            
            UnoSkip = "<:block:452172078859419678>",
            UnoReverse = "<:reverse:452172078796242964>",
            AddTwo = "<:plus2:452172078599241739>",
            AddFour = "<:plus4:452196173898448897>",
            UnoWild = "<:colors:452172078028947457>",
            
            BronzeIcon = "<:bronze:453367514550894602>",
            SilverIcon = "<:silver:453367514588774400>",
            GoldIcon = "<:gold:453368658303909888>";


        public static readonly string[] NumberCircle = new string[]
        {
            "<:0circle:445021371127562280>",
            "<:1circle:445021372356231186>",
            "<:2circle:445021372889038858>",
            "<:3circle:445021372213886987>",
            "<:4circle:445021372905947138>",
            "<:5circle:445021373136502785>",
            "<:6circle:445021373405069322>",
            "<:7circle:445021372687581185>",
            "<:8circle:445021372465283098>",
            "<:9circle:445021373245554699>",
        };

        public static readonly string[] LetterCircle = new string[]
        {
            "<:circleA:446196337831313409>",
            "<:circleB:446196339660029952>",
            "<:circleC:446196339471024129>",
            "<:circleD:446196339450314753>",
            "<:circleE:446196339987054592>",
        };

        public static readonly string[] ColorSquare = new string[]
        {
            "<:redsq:452165349719277579>",
            "<:bluesq:452165349790580746>",
            "<:greensq:452165350155616257>",
            "<:yellowsq:452165350184976384>",
            "<:blacksq:452196173026164739>",
        };
    }
}
