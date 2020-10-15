using System.Collections.Generic;
using PacManBot.Constants;

namespace PacManBot.Commands
{
    public static class Categories
    {
        public const string
            Dev = CustomEmoji.Discord + " Developer",
            Mod = CustomEmoji.Staff + " Mod",
            General = "📁 General",
            Games = CustomEmoji.GameCube + " Games",
            Misc = "💡 Misc";

        public static readonly IReadOnlyList<string> Order
            = new string[] { Dev, General, Games, Misc, Mod };
    }
}
