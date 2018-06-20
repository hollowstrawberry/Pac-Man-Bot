using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Discord;
using Discord.Rest;
using Discord.Commands;
using PacManBot.Commands;
using PacManBot.Extensions;

namespace PacManBot.Services
{
    public class ScriptingService // Thanks to oatmeal and amibu
    {
        private readonly IServiceProvider provider;
        private readonly LoggingService logger;
        private readonly ScriptOptions scriptOptions;


        public ScriptingService(IServiceProvider provider)
        {
            this.provider = provider;
            logger = provider.Get<LoggingService>();

            scriptOptions = ScriptOptions.Default
                .WithImports(
                    "System", "System.IO", "System.Text", "System.Linq", "System.Diagnostics", "System.Threading.Tasks",
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


        public async Task EvalAsync(string code, BaseCustomModule module)
        {
            try
            {
                code = code.Trim(' ', '`', '\n');
                if (code.StartsWith("cs\n")) code = code.Remove(0, 3); // C# code block in Discord

                if (!code.Contains(";")) // Treats a single expression as a message to send in chat
                {
                    code = "await ReplyAsync($\"{" + code + "}\");";
                }

                await CSharpScript.EvaluateAsync(code, scriptOptions, module);
                await logger.Log(LogSeverity.Info, $"Successfully executed code in channel {module.Context.Channel.FullName()}:\n{code}");
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
    }
}
