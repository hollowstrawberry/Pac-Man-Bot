using Discord;
using Discord.Commands;
using Discord.WebSocket;
using static PacManBot.Modules.PacManModule.Game;

namespace PacManBot
{
    public static class Utils
    {
        //Conditional strings to help with complex text concatenation
        public static string If(this string text, bool condition) => condition ? text : "";
        public static string Unless(this string text, bool condition) => condition ? "" : text;

        //2-dimensional array length
        public static int LengthX<T>(this T[,] array) => array.GetLength(0);
        public static int LengthY<T>(this T[,] array) => array.GetLength(1);



        //Discord utilities

        public static bool BotHas(this SocketCommandContext context, ChannelPermission permission)
        {
            return context.Guild != null && context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel).Has(permission);
        }


        //Game utilities

        public static Dir Opposite(this Dir dir)
        {
            switch (dir)
            {
                case Dir.up:    return Dir.down;
                case Dir.down:  return Dir.up;
                case Dir.left:  return Dir.right;
                case Dir.right: return Dir.left;
                default: return Dir.none;
            }
        }

        public static Pos OfLength(this Dir dir, int num) //Converts a direction into a position vector
        {
            if (num < 0) num = 0;
            Pos pos = new Pos(0, 0);
            for (int i = 0; i < num; i++) pos += dir;
            return pos;
        }
    }
}