using System.Collections.Generic;
using System.Linq;
using DSharpPlus.Entities;
using PacManBot.Extensions;
using Range = PacManBot.Utils.Range;

namespace PacManBot.Constants
{
    /// <summary>
    /// All custom emoji that this bot has access to from the Pac-Man support server.
    /// </summary>
    public static class CustomEmoji
    {
        public static readonly DiscordGuildEmoji
            ECheck = Check.ToEmoji(),
            ECross = Cross.ToEmoji(),
            ELoading = Loading.ToEmoji(),
            EHelp = Help.ToEmoji(),
            EGitHub = GitHub.ToEmoji(),
            EBlobDance = BlobDance.ToEmoji();


        public static readonly IReadOnlyList<string> Number =
            new Range(10).Select(x => x.ToString() + "️⃣").ToArray();


        public const string
            Check = "<:check:410612082929565696>",
            Cross = "<:cross:410612082988285952>",
            Loading = "<a:loading:410612084527595520>",
            PacMan = "<a:pacman:409803570544902144>",
            Help = "<:help:438481218674229248>",
            BlobDance = "<a:danceblob:751079963473477693>",

            Discord = "<:discord:409811304103149569>",
            GitHub = "<:github:409803419717599234>",
            Staff = "<:staff:412019879772815361>",
            Thinkxel = "<:thinkxel:409803420308996106>",
            Empty = "<:Empty:445680384592576514>",

            TTTx = "<:TTTx:445729766952402944>",
            TTTxHL = "<a:TTTxHL:491828381990649866>",
            TTTo = "<:TTTo:445729766780436490>",
            TTToHL = "<a:TTToHL:491828125269884928>",
            C4red = "<:C4red:445683639137599492>",
            C4redHL = "<a:C4RedHL:491824083122782209>",
            C4blue = "<:C4blue:445683639817207813>",
            C4blueHL = "<a:C4blueHL:491824083185696768>",
            BlackCircle = "<:black:451507461556404224>",
            
            UnoSkip = "<:block:452172078859419678>",
            UnoReverse = "<:reverse:452172078796242964>",
            AddTwo = "<:plus2:452172078599241739>",
            AddFour = "<:plus4:452196173898448897>",
            UnoWild = "<:colors:452172078028947457>",

            RedSquare = "<:redsq:452165349719277579>",
            BlueSquare = "<:bluesq:452165349790580746>",
            GreenSquare = "<:greensq:452165350155616257>",
            YellowSquare = "<:yellowsq:452165350184976384>",
            OrangeSquare = "<:ornsq:456684646554664972>",
            WhiteSquare = "<:whsq:456684646403538946>",
            BlackSquare = "<:blacksq:452196173026164739>",

            BronzeIcon = "<:bronze:453367514550894602>",
            SilverIcon = "<:silver:453367514588774400>",
            GoldIcon = "<:gold:453368658303909888>",
            PetRight = "<:petright:491790787856826400>",
            PetLeft = "<:petleft:491790787877535745>",
            
            Life = "<a:hp:496668550698893322>",
            Mana = "<a:mp:496668551231307786>";


        public static readonly string[] NumberCircle =
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

        public static readonly string[] LetterCircle =
        {
            "<:circleA:446196337831313409>",
            "<:circleB:446196339660029952>",
            "<:circleC:446196339471024129>",
            "<:circleD:446196339450314753>",
            "<:circleE:446196339987054592>",
        };
    }
}
