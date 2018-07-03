namespace PacManBot.Constants
{
    public enum TimePeriod
    {
        All = -1,
        Month = 24 * 30,
        Week = 24 * 7,
        Day = 24,
        A = All, M = Month, W = Week, D = Day // To be parsed from a string
    }
}
