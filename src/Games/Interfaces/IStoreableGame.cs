using System;

namespace PacManBot.Games
{
    public interface IStoreableGame : IBaseGame
    {
        string FilenameKey { get; } // Word used to identify the game type in the filename
        void PostDeserialize(IServiceProvider services);
    }
}
