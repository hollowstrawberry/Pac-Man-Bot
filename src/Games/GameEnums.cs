namespace PacManBot.Games
{
    public enum State
    {
        Active,
        Completed,
        Cancelled,
        Lose,
        Win,
    }


    public enum Player
    {
        First, Second, Third, Fourth, Fifth, Sixth, Seventh, Eighth, Nineth, Tenth,
        None = -1,
        Tie = -2,
    }


    public enum Dir
    {
        None,
        Up,
        Left,
        Down,
        Right,
    }
}
