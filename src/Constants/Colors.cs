using DSharpPlus.Entities;

namespace PacManBot.Constants
{
    /// <summary>
    /// Contains the color scheme used by the bot, often in embeds.
    /// </summary>
    public static class Colors
    {
        public static readonly DiscordColor
            // Discord emotes color scheme
            Red    = new DiscordColor(221, 46, 68),
            Blue   = new DiscordColor(85, 172, 238),
            Green  = new DiscordColor(120, 177, 89),
            Yellow = new DiscordColor(253, 203, 88),
            Purple = new DiscordColor(170, 142, 214),
            Orange = new DiscordColor(244, 144, 12),
            White  = new DiscordColor(230, 231, 232),
            Black  = new DiscordColor(41, 47, 51),

            // Custom colors
            Gray      = new DiscordColor(150, 150, 150),
            PureWhite = new DiscordColor(255, 255, 255),
            DarkBlack = new DiscordColor(20, 26, 30),
            PacManYellow = new DiscordColor(241, 195, 15);
    }
}
