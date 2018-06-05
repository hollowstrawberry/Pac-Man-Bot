using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Discord;
using Discord.Rest;
using Discord.Commands;
using Discord.WebSocket;

namespace PacManBot.Services
{
    public class ScriptingService // Thanks to oatmeal and amibu
    {
        private readonly IServiceProvider provider;
        private readonly DiscordShardedClient shardedClient;
        private readonly LoggingService logger;
        private readonly StorageService storage;

        private readonly ScriptOptions scriptOptions;


        public ScriptingService(IServiceProvider provider)
        {
            this.provider = provider;
            this.shardedClient = provider.Get<DiscordShardedClient>();
            this.logger = provider.Get<LoggingService>();
            this.storage = provider.Get<StorageService>();

            scriptOptions = ScriptOptions.Default
                .WithImports(
                    "System", "System.IO", "System.Threading.Tasks", "System.Collections.Generic", "System.Linq", "System.Text.RegularExpressions", "System.Diagnostics",
                    "Discord", "Discord.WebSocket", "Discord.Commands",
                    "PacManBot", "PacManBot.Constants", "PacManBot.Services", "PacManBot.Modules", "PacManBot.Games",
                    "PacManBot.Utils", "PacManBot.Games.GameUtils"
                )
                .WithReferences(
                    typeof(ShardedCommandContext).Assembly,
                    typeof(StorageService).Assembly,
                    typeof(RestUserMessage).Assembly,
                    typeof(IMessageChannel).Assembly
                );
        }


        public async Task Eval(string code, ShardedCommandContext context)
        {
            try
            {
                code = code.Trim(' ', '`', '\n');
                if (code.StartsWith("cs\n")) code = code.Remove(0, 3); //C# code block in Discord

                if (!code.Contains(";")) //Treats a single expression as a message to send in chat
                {
                    code = "await ReplyAsync($\"{" + code + "}\");";
                }

                string postCode = "\nTask ReplyAsync(string msg) => Context.Channel.SendMessageAsync(msg);";

                await CSharpScript.EvaluateAsync(code + postCode, scriptOptions, new ScriptArgs(context, provider, shardedClient, logger, storage));
                await logger.Log(LogSeverity.Info, $"Successfully executed code in channel {context.Channel.FullName()}:\n{code}");
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
    }



    public class ScriptArgs : EventArgs
    {
        public readonly ShardedCommandContext Context;
        public readonly IServiceProvider provider;
        DiscordShardedClient shardedClient;
        public readonly LoggingService logger;
        public readonly StorageService storage;

        public ScriptArgs(ShardedCommandContext Context, IServiceProvider provider, DiscordShardedClient shardedClient, LoggingService logger, StorageService storage)
        {
            this.Context = Context;
            this.provider = provider;
            this.shardedClient = shardedClient;
            this.logger = logger;
            this.storage = storage;
        }
    }
}
