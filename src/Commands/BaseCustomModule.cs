using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PacManBot.Services;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Commands
{
    /// <summary>
    /// The base for Pac-Man Bot modules, containing their main services and some utilities.
    /// </summary>
    /// <remarks>Its members are public to be accessed as globals from within an evaluated script.</remarks>
    public abstract class BaseCustomModule : ModuleBase<ShardedCommandContext>
    {
        /// <summary>Consistent and sane <see cref="RequestOptions"/> to be used in most Discord requests.</summary>
        public static readonly RequestOptions DefaultOptions = Bot.DefaultOptions;


        /// <summary>All of this program's services, required to supply new objects such as games.</summary>
        public IServiceProvider Services { get; }

        /// <summary>Contents used throughout the bot.</summary>
        public BotContent Content { get; }

        /// <summary>Logs everything in the console and on disk.</summary>
        public LoggingService Logger { get; }

        /// <summary>Gives access to the bot's database.</summary>
        public StorageService Storage { get; }

        /// <summary>Gives access to active games.</summary>
        public GameService Games { get; }

        /// <summary>The relative prefix used in this context. Might be empty in DMs and other cases.</summary>
        public string Prefix { get; private set; }

        /// <summary>The prefix accepted in this context, even if none is necessary.</summary>
        public string AbsolutePrefix { get; private set; }



        protected BaseCustomModule(IServiceProvider services)
        {
            Services = services;
            Logger = services.Get<LoggingService>();
            Storage = services.Get<StorageService>();
            Games = services.Get<GameService>();
            Content = services.Get<BotConfig>().Content;
        }


        protected override void BeforeExecute(CommandInfo command)
        {
            AbsolutePrefix = Storage.GetGuildPrefix(Context.Guild);
            Prefix = Storage.RequiresPrefix(Context) ? AbsolutePrefix : "";
        }


        protected override async void AfterExecute(CommandInfo command)
        {
            await Logger.Log(LogSeverity.Verbose, LogSource.Command,
                             $"Executed {command.Name} for {Context.User.FullName()} in {Context.Channel.FullName()}");
        }


        /// <summary>Sends a message in the current context, using the default options if not specified.</summary>
        protected override async Task<IUserMessage> ReplyAsync(string message = null, bool isTTS = false, Embed embed = null, RequestOptions options = null)
        {
            return await base.ReplyAsync(message, isTTS, embed, options ?? DefaultOptions);
        }


        /// <summary>Sends a message in the current context, containing text and an embed, and using the default options if not specified.</summary>
        public async Task<IUserMessage> ReplyAsync(object text, EmbedBuilder embed, RequestOptions options = null)
            => await ReplyAsync(text?.ToString(), false, embed?.Build(), options);


        /// <summary>Sends a message in the current context containing only text, and using the default options if not specified.</summary>
        public async Task<IUserMessage> ReplyAsync(object text, RequestOptions options = null)
            => await ReplyAsync(text.ToString(), false, null, options);


        /// <summary>Sends a message in the current context containing only an embed, and using the default options if not specified.</summary>
        public async Task<IUserMessage> ReplyAsync(EmbedBuilder embed, RequestOptions options = null)
            => await ReplyAsync(null, false, embed.Build(), options);



        /// <summary>Reacts to the command's calling message with a check or cross.</summary>
        public async Task AutoReactAsync(bool success = true)
            => await Context.Message.AutoReactAsync(success);


        /// <summary>Creates an instance of a different module. This shouldn't ever be used. I'm a madman.</summary>
        public TModule GetModule<TModule>() where TModule : BaseCustomModule
            => ModuleBuilder<TModule, ShardedCommandContext>.Create(Context, Services);
    }
}
