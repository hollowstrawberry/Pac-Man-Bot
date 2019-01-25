namespace PacManBot.Constants
{
    /// <summary>
    /// The source of a <see cref="Services.LoggingService"/> entry.
    /// </summary>
    public static class LogSource
    {
        public const string
            Bot = "Bot",
            Game = "Games",
            Command = "Commands",
            Storage = "Storage",
            Scheduling = "Scheduling",
            Owner = "Owner",
            Eval = "Eval";
    }
}
