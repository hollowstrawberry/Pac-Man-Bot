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
        public static readonly IEmote
            PacMan = "<a:pacman:409803570544902144>".ToEmote(),
            Help = "<:help:438481218674229248>".ToEmote(),
            Wait = "<:wait:438480194991554561>".ToEmote(),
            Discord = "<:discord:409811304103149569>".ToEmote(),
            GitHub = "<:github:409803419717599234>".ToEmote(),
            Staff = "<:staff:412019879772815361>".ToEmote(),
            Check = "<:check:410612082929565696>".ToEmote(),
            Cross = "<:cross:410612082988285952>".ToEmote(),
            Loading = "<a:loading:410612084527595520>".ToEmote(),
            Dance = "<a:danceblobfast:439575722567401479>".ToEmote();
    }
}
