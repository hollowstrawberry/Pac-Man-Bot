using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PacManBot.Constants;
using PacManBot.Extensions;
using PacManBot.Games;

namespace PacManBot.Services
{
    /// <summary>
    /// Manages active game instances from this bot as well as their files on disk.
    /// </summary>
    public class GameService
    {
        private static readonly IEnumerable<(string key, Type type)> StoreableGameTypes = ReflectionExtensions.AllTypes
            .SubclassesOf<IStoreableGame>()
            .Select(t => (t.CreateInstance<IStoreableGame>().FilenameKey, t))
            .OrderByDescending(t => t.FilenameKey.Length)
            .ToArray();

        private static readonly JsonSerializerSettings GameJsonSettings = new JsonSerializerSettings
        {
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            TypeNameHandling = TypeNameHandling.Auto,
        };

        private readonly IServiceProvider services;
        private readonly LoggingService log;
        private readonly ConcurrentDictionary<ulong, IChannelGame> games;
        private readonly ConcurrentDictionary<(ulong, Type), IUserGame> userGames;


        /// <summary>Enumerates through all active channel-specific games concurrently.</summary>
        public IEnumerable<IChannelGame> AllChannelGames => games.Select(x => x.Value);
        /// <summary>Enumerates through all active user-specific games concurrently.</summary>
        public IEnumerable<IUserGame> AllUserGames => userGames.Select(x => x.Value);
        /// <summary>Enumerates through all active games of any type.</summary>
        public IEnumerable<IBaseGame> AllGames => AllChannelGames.Cast<IBaseGame>().Concat(AllUserGames.Cast<IBaseGame>());


        public GameService(IServiceProvider services, LoggingService log)
        {
            this.services = services;
            this.log = log;

            games = new ConcurrentDictionary<ulong, IChannelGame>();
            userGames = new ConcurrentDictionary<(ulong, Type), IUserGame>();
        }




        /// <summary>Retrieves the active game for the specified channel. Null if not found.</summary>
        public IChannelGame GetForChannel(ulong channelId)
        {
            return games.TryGetValue(channelId, out var game) ? game : null;
        }


        /// <summary>Retrieves the active game for the specified channel, cast to the desired type.
        /// Null if not found or if it is the wrong type.</summary>
        public TGame GetForChannel<TGame>(ulong channelId) where TGame : class, IChannelGame
        {
            return GetForChannel(channelId) as TGame;
        }


        /// <summary>Retrieves the specified user's game of the desired type. Null if not found.</summary>
        public TGame GetForUser<TGame>(ulong userId) where TGame : class, IUserGame
        {
            return userGames.TryGetValue((userId, typeof(TGame)), out var game) ? (TGame)game : null;
        }


        /// <summary>Retrieves the specified user's game of the desired type. Null if not found.</summary>
        public IUserGame GetForUser(ulong userId, Type type)
        {
            return userGames.TryGetValue((userId, type), out var game) ? game : null;
        }


        /// <summary>Adds a new game to the collection of channel games or user games.</summary>
        public void Add(IBaseGame game)
        {
            if (game is IUserGame uGame) userGames.TryAdd((uGame.OwnerId, uGame.GetType()), uGame);
            else if (game is IChannelGame cGame) games.TryAdd(cGame.ChannelId, cGame);
        }


        /// <summary>Permanently deletes a game from the collection of channel games or user games, 
        /// as well as its savefile if there is one.</summary>
        public void Remove(IBaseGame game, bool doLog = true)
        {
            if (game == null) return;

            game.CancelRequests();
            bool success = false;

            if (game is IUserGame uGame)
            {
                success = userGames.TryRemove((uGame.OwnerId, uGame.GetType()));
            }
            else if (game is IChannelGame cGame)
            {
                success = games.TryRemove(cGame.ChannelId);
            }

            if (game is IStoreableGame sGame && File.Exists(sGame.GameFile()))
            {
                try { File.Delete(sGame.GameFile()); }
                catch (IOException e) { log.Exception($"Couldn't delete {sGame.GameFile()}", e, LogSource.Game); }
            }

            if (success && doLog)
            {
                log.Verbose($"Removed {game.GetType().Name} at {game.IdentifierId()}", LogSource.Game);
            }
        }


        /// <summary>Stores a backup of the game on disk asynchronously, to be loaded the next time the bot starts.</summary>
        public async Task SaveAsync(IStoreableGame game)
        {
            game.LastPlayed = DateTime.Now;
            await File.WriteAllTextAsync(game.GameFile(), JsonConvert.SerializeObject(game, GameJsonSettings), Encoding.UTF8);
        }




        /// <summary>Reload the entire game collection from disk.</summary>
        public async Task LoadGamesAsync()
        {
            games.Clear();
            userGames.Clear();

            if (!Directory.Exists(Files.GameFolder))
            {
                Directory.CreateDirectory(Files.GameFolder);
                log.Warning($"Created missing directory \"{Files.GameFolder}\"", LogSource.Game);
                return;
            }

            uint fail = 0;
            bool firstFail = true;

            foreach (string file in Directory.GetFiles(Files.GameFolder).Where(f => f.EndsWith(Files.GameExtension)))
            {
                try
                {
                    Type gameType = StoreableGameTypes.First(x => file.Contains(x.key)).type;
                    string json = await File.ReadAllTextAsync(file);
                    var game = (IStoreableGame)JsonConvert.DeserializeObject(json, gameType, GameJsonSettings);
                    game.PostDeserialize(services);

                    if (game is IUserGame uGame) userGames.TryAdd((uGame.OwnerId, uGame.GetType()), uGame);
                    else if (game is IChannelGame cGame) games.TryAdd(cGame.ChannelId, cGame);
                }
                catch (Exception e)
                {
                    log.Log(
                        $"Couldn't load game at {file}: {(firstFail ? e.ToString() : e.Message)}",
                        firstFail ? LogLevel.Error : LogLevel.Trace, LogSource.Game);
                    fail++;
                    firstFail = false;
                }
            }

            log.Info(
                $"Loaded {games.Count + userGames.Count} games{$" with {fail} errors".If(fail > 0)}",
                LogSource.Game);
        }
    }
}
