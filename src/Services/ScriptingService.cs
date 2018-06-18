using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Discord;
using Discord.Rest;
using Discord.Commands;
using PacManBot.Extensions;

namespace PacManBot.Services
{
    public class ScriptingService // Thanks to oatmeal and amibu
    {
        private readonly IServiceProvider provider;
        private readonly LoggingService logger;
        private readonly StorageService storage;

        private readonly ScriptOptions scriptOptions;


        public ScriptingService(IServiceProvider provider)
        {
            this.provider = provider;
            logger = provider.Get<LoggingService>();
            storage = provider.Get<StorageService>();

            scriptOptions = ScriptOptions.Default
                .WithImports(
                    "System", "System.IO", "System.Linq", "System.Diagnostics", "System.Threading.Tasks",
                    "System.Collections.Generic", "System.Text.RegularExpressions",
                    "Discord", "Discord.Rest", "Discord.Commands", "Discord.WebSocket",
                    "PacManBot", "PacManBot.Games", "PacManBot.Utils", "PacManBot.Commands", "PacManBot.Services", "PacManBot.Extensions"
                )
                .WithReferences(
                    typeof(ShardedCommandContext).Assembly,
                    typeof(StorageService).Assembly,
                    typeof(RestUserMessage).Assembly,
                    typeof(IMessageChannel).Assembly
                );
        }


        public async Task EvalAsync(string code, ShardedCommandContext context)
        {
            try
            {
                code = code.Trim(' ', '`', '\n');
                if (code.StartsWith("cs\n")) code = code.Remove(0, 3); //C# code block in Discord

                if (!code.Contains(";")) //Treats a single expression as a message to send in chat
                {
                    code = "await ReplyAsync($\"{" + code + "}\");";
                }

                string postCode = "\nTask ReplyAsync(string message = null, bool isTTS = false, Embed embed = null, RequestOptions options = null) " +
                                  "=> Context.Channel.SendMessageAsync(message, isTTS, embed, options);";

                await CSharpScript.EvaluateAsync(code + postCode, scriptOptions, new ScriptArgs(context, provider, logger, storage));
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
        public readonly LoggingService logger;
        public readonly StorageService storage;

        public ScriptArgs(ShardedCommandContext Context, IServiceProvider provider, LoggingService logger, StorageService storage)
        {
            this.Context = Context;
            this.provider = provider;
            this.logger = logger;
            this.storage = storage;
        }
    }
}
