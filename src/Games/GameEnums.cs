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


    /// <summary>The player of a <see cref="MultiplayerGame"/>.</summary>
    public enum Player
    {
        First, Second, Third, Fourth, Fifth, Sixth, Seventh, Eighth, Nineth, Tenth,
        None = -1,
        Tie = -2,
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
