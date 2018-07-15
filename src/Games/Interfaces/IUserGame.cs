using Discord;

namespace PacManBot.Games
{
    public interface IUserGame : IBaseGame
    {
        ulong OwnerId { get; }
        IUser Owner { get; }
    }
}
