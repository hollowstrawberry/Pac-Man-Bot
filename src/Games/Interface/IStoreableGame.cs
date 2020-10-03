using System;

namespace PacManBot.Games
{
    /// <summary>
    /// Identifies a game type as storeable, allowing calls to <see cref="Services.GameService.SaveAsync(IStoreableGame)"/>
    /// and automatically deserializing stored instances that match the <see cref="FilenameKey"/> on start-up.
    /// </summary>
    public interface IStoreableGame : IBaseGame
    {
        /// <summary>Used to uniquely identify this game type when stored in a file.</summary>
        string FilenameKey { get; }

        /// <summary>Runs after deserialization, to load runtime data that wasn't serialized.</summary>
        void PostDeserialize(IServiceProvider services);
    }
}
