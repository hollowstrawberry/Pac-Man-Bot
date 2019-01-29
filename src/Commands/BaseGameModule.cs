using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Extensions;
using PacManBot.Games;
using PacManBot.Services;

namespace PacManBot.Commands
{
    /// <summary>
    /// The base for a module that controls a game.
    /// </summary>
    /// <typeparam name="TGame">The game type that this module controls.</typeparam>
    [PmRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.EmbedLinks |
                        ChannelPermission.UseExternalEmojis | ChannelPermission.AddReactions)]
    public abstract class BaseGameModule<TGame> : BaseModule
        where TGame :  IBaseGame
    {
        /// <summary>All services used to create new games.</summary>
        public IServiceProvider Services { get; set; }

        /// <summary>Gives access to command information.</summary>
        public PmCommandService Commands { get; set; }

        /// <summary>Gives access to active games.</summary>
        public GameService Games { get; set; }

        /// <summary>The active game in the channel, if any.</summary>
        public IChannelGame ExistingGame => Games.GetForChannel(Context.Channel.Id);

        /// <summary>The active game for this context.</summary>
        public TGame Game { get; private set; }
            

        public BaseGameModule()
        {
            Game = typeof(TGame).IsAssignableFrom(typeof(IUserGame))
                ? (TGame)Games.GetForUser(Context.User.Id, typeof(TGame))
                : (TGame)Games.GetForChannel(Context.Channel.Id);
        }


        /// <summary>Deletes the game for this context from the bot.</summary>
        public void RemoveGame()
        {
            if (Game != null) Games.Remove(Game);
        }

        /// <summary>Adds a new game for this context to the bot.</summary>
        public void CreateGame(TGame game)
        {
            Games.Add(Game = game);
        }
    }
}
