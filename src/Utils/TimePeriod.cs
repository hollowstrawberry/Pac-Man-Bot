namespace PacManBot.Utils
{
    /// <summary>
    /// A period indicated in hours for the score leaderboard, useful to parse from a string.
    /// </summary>
    public enum TimePeriod
    {
        All = -1,
        Month = 24 * 30,
        Week = 24 * 7,
        Day = 24,
        A = All, M = Month, W = Week, D = Day
    }
}
