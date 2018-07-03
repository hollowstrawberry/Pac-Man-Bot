using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PacManBot.Services;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Commands
{
    public abstract class BaseCustomModule : ModuleBase<ShardedCommandContext>
    {
        public static readonly RequestOptions DefaultOptions = Bot.DefaultOptions;

        public IServiceProvider Services { get; }
        public LoggingService Logger { get; }
        public StorageService Storage { get; }

        public string Prefix { get; private set; }
        public string AbsolutePrefix { get; private set; }


        protected BaseCustomModule(IServiceProvider services)
        {
            Services = services;
            Logger = services.Get<LoggingService>();
            Storage = services.Get<StorageService>();
        }


        protected override void BeforeExecute(CommandInfo command)
        {
            Prefix = Storage.GetPrefixOrEmpty(Context.Guild);
            AbsolutePrefix = string.IsNullOrEmpty(Prefix) ? Storage.DefaultPrefix : Prefix;
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


        public async Task AutoReactAsync(bool success = true)
            => await Context.Message.AutoReactAsync(success);


        public TModule GetModule<TModule>() where TModule : BaseCustomModule
            => ModuleBuilder<TModule, ShardedCommandContext>.Create(Context, Services);
    }
}
