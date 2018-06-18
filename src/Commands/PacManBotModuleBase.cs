using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PacManBot.Services;
using PacManBot.Extensions;

namespace PacManBot.Commands
{
    public abstract class PacManBotModuleBase : ModuleBase<ShardedCommandContext>
    {
        protected static readonly RequestOptions DefaultOptions = Bot.DefaultOptions;

        protected readonly LoggingService logger;
        protected readonly StorageService storage;
        
        protected string Prefix { get; private set; }
        protected string AbsolutePrefix { get; private set; }

        protected PacManBotModuleBase(LoggingService logger, StorageService storage) : base()
        {
            this.logger = logger;
            this.storage = storage;
        }



        protected override void BeforeExecute(CommandInfo command)
        {
            Prefix = storage.GetPrefixOrEmpty(Context.Guild);
            AbsolutePrefix = string.IsNullOrEmpty(Prefix) ? storage.DefaultPrefix : Prefix;
        }


        protected override async void AfterExecute(CommandInfo command)
        {
            await logger.Log(LogSeverity.Verbose, LogSource.Command,
                             $"Executed {command.Name} for {Context.User.FullName()} in {Context.Channel.FullName()}");
        }


        protected override async Task<IUserMessage> ReplyAsync(string message = null, bool isTTS = false, Embed embed = null, RequestOptions options = null)
            => await base.ReplyAsync(message, isTTS, embed, options ?? DefaultOptions);

        protected async Task<IUserMessage> ReplyAsync(string message, EmbedBuilder embed, RequestOptions options = null)
            => await ReplyAsync(message, false, embed?.Build(), options);

        protected async Task<IUserMessage> ReplyAsync(string message, RequestOptions options = null)
            => await ReplyAsync(message, false, null, options);

        protected async Task<IUserMessage> ReplyAsync(EmbedBuilder embed, RequestOptions options = null)
            => await ReplyAsync(null, false, embed?.Build(), options);


        protected async Task AutoReactAsync(bool success = true)
            => await Context.Message.AutoReactAsync(success);
    }
}
