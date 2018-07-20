
namespace PacManBot.Games
{
    /// <summary>Indicates whether a <see cref="IBaseGame"/> is ongoing, or the reason that it has ended for.</summary>
    public enum State
    {
        Active,
        Completed,
        Cancelled,
        Lose,
        Win,
    }


    /// <summary>The four cardinal directions, useful with <see cref="Pos"/>.</summary>
    public enum Dir
    {
        None,
        Up,
        Left,
        Down,
        Right,
    }
}
