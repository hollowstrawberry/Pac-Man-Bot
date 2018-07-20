
namespace PacManBot.Games
{
    /// <summary>Indicates whether a game is ongoing, or why it ended.</summary>
    public enum State
    {
        Active,
        Completed,
        Cancelled,
        Lose,
        Win,
    }
}
