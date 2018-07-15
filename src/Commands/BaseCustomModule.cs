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
    /// The base for Pac-Man Bot modules, containing its services and some utilities.
    /// Its members are public to be accessed as globals from within an evaluated script.
    /// </summary>
    public abstract class BaseCustomModule : ModuleBase<ShardedCommandContext>
    {
        /// <summary>Consistent and sane <see cref="RequestOptions"/> to be used in most Discord requests.</summary>
        public static readonly RequestOptions DefaultOptions = Bot.DefaultOptions;


        /// <summary>Provides all of this program's services.</summary>
        public IServiceProvider Services { get; }

        /// <summary>Logs everything in the console and on disk.</summary>
        public LoggingService Logger { get; }

        /// <summary></summary>
        public StorageService Storage { get; }

        /// <summary>The relative prefix used in this context. Might be empty in DMs and other cases.</summary>
        public string Prefix { get; private set; }

        /// <summary>The prefix accepted in this context, even if none is necessary.</summary>
        public string AbsolutePrefix { get; private set; }



        protected BaseCustomModule(IServiceProvider services)
        {
            Services = services;
            Logger = services.Get<LoggingService>();
            Storage = services.Get<StorageService>();
        }


        protected override void BeforeExecute(CommandInfo command)
        {
            AbsolutePrefix = Storage.GetPrefix(Context.Guild);
            Prefix = Context.Guild == null || Storage.NoPrefixChannel(Context.Channel.Id) ? "" : AbsolutePrefix;
        }


        protected override async void AfterExecute(CommandInfo command)
        {
            await Logger.Log(LogSeverity.Verbose, LogSource.Command,
                             $"Executed {command.Name} for {Context.User.FullName()} in {Context.Channel.FullName()}");
        }


        protected override async Task<IUserMessage> ReplyAsync(string message = null, bool isTTS = false, Embed embed = null, RequestOptions options = null)
        {
            return await base.ReplyAsync(message, isTTS, embed, options ?? DefaultOptions);
        }




        public async Task<IUserMessage> ReplyAsync(string message, EmbedBuilder embed, RequestOptions options = null)
            => await ReplyAsync(message, false, embed?.Build(), options);

        public async Task<IUserMessage> ReplyAsync(string message, RequestOptions options = null)
            => await ReplyAsync(message, false, null, options);

        public async Task<IUserMessage> ReplyAsync(EmbedBuilder embed, RequestOptions options = null)
            => await ReplyAsync(null, false, embed?.Build(), options);


        /// <summary>Reacts to the command's caller message with a check or cross.</summary>
        public async Task AutoReactAsync(bool success = true)
            => await Context.Message.AutoReactAsync(success);


        /// <summary>Creates an instance of a different module. This shouldn't ever be used. I'm a madman.</summary>
        public TModule GetModule<TModule>() where TModule : BaseCustomModule
            => ModuleBuilder<TModule, ShardedCommandContext>.Create(Context, Services);
    }
}
