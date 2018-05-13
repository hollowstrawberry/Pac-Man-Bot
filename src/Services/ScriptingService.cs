using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Discord;
using Discord.Rest;
using Discord.Commands;

namespace PacManBot.Services
{
    public class ScriptingService // Thanks to oatmeal and amibu
    {
        private readonly ScriptOptions scriptOptions;
        private readonly StorageService storage;
        private readonly LoggingService logger;
        private readonly SchedulingService scheduling;


        public ScriptingService(StorageService storage, LoggingService logger, SchedulingService scheduling)
        {
            this.storage = storage;
            this.logger = logger;
            this.scheduling = scheduling;

            scriptOptions = ScriptOptions.Default
                .WithImports(
                    "System", "System.IO", "System.Threading.Tasks", "System.Collections.Generic", "System.Linq", "System.Text.RegularExpressions",
                    "Discord", "Discord.WebSocket", "Discord.Commands",
                    "PacManBot", "PacManBot.Constants", "PacManBot.Utils", "PacManBot.Services", "PacManBot.Modules", "PacManBot.Games"
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

                await CSharpScript.EvaluateAsync(code + postCode, scriptOptions, new ScriptArgs(context, storage, logger, scheduling));
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
        public readonly StorageService storage;
        public readonly LoggingService logger;
        public readonly SchedulingService scheduling;

        public ScriptArgs(ShardedCommandContext Context, StorageService storage, LoggingService logger, SchedulingService scheduling)
        {
            this.Context = Context;
            this.storage = storage;
            this.logger = logger;
            this.scheduling = scheduling;
        }
    }
}
