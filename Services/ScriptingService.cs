using System;
using System.Linq;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace PacManBot.Services
{
    public class ScriptingService // Thanks to oatmeal and amibu
    {
        private readonly ScriptOptions scriptOptions;
        private readonly StorageService storage;
        private readonly LoggingService logger;

        public ScriptingService(StorageService storage, LoggingService logger)
        {
            this.storage = storage;
            this.logger = logger;

            scriptOptions = ScriptOptions.Default
                .WithImports(
                    "System",
                    "System.Threading.Tasks",
                    "Discord", "Discord.WebSocket",
                    "Discord.Commands",
                    "PacManBot",
                    "PacManBot.Services",
                    "PacManBot.Constants",
                    "PacManBot.Modules"
                )
                .WithReferences(
                    typeof(SocketCommandContext).Assembly,
                    typeof(StorageService).Assembly,
                    typeof(IMessageChannel).Assembly,
                    typeof(RestUserMessage).Assembly
                );
        }

        public void Eval(string code, SocketCommandContext context)
        {
            string baseCode = "\nTask ReplyAsync(string msg) => Context.Channel.SendMessageAsync(msg);";

            code = code.Trim(' ', '`', '\n');
            if (code.StartsWith("cs\n")) code = code.Remove(0, 3); //C# code block in Discord

            if (!code.EndsWith(";") && !code.EndsWith("}")) //Treats the last expression as a result to send in chat
            {
                string previousExpressions = "";
                string lastExpression = (code.LastIndexOf(';') > code.LastIndexOf('}') ? code.Split(';'): code.Split('}')).Last().Trim(' ', '\n');
                if (code.Contains(";") || code.Contains("}")) previousExpressions = code.Remove(code.LastIndexOf(lastExpression)); //All but the last expression
                code = previousExpressions + "ReplyAsync($\"{" + lastExpression + "}\");";
            }

            logger.Log(LogSeverity.Debug, $"Evaluating code \"{code}\" in channel {context.FullChannelName()}");

            Script<object> script = CSharpScript.Create(code + baseCode, scriptOptions, typeof(ScriptArgs));
            ScriptArgs scriptArgs = new ScriptArgs(context, storage, logger);
            script.Compile();
            script.RunAsync(scriptArgs).Wait();

            logger.Log(LogSeverity.Debug, $"Successfully executed code");
        }
    }

    public class ScriptArgs : EventArgs
    {
        public readonly SocketCommandContext Context;
        public readonly StorageService storage;
        public readonly LoggingService logger;

        public ScriptArgs(SocketCommandContext Context, StorageService storage, LoggingService logger)
        {
            this.Context = Context;
            this.storage = storage;
            this.logger = logger;
        }
    }
}