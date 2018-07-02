using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting;

namespace PacManBot.Services
{
    public class ScriptingService // Thanks to oatmeal and amibu
    {
        private readonly ScriptOptions scriptOptions;


        public ScriptingService()
        {
            scriptOptions = ScriptOptions.Default
                .WithImports(
                    "System", "System.IO", "System.Text", "System.Linq", "System.Reflection", "System.Diagnostics",
                    "System.Threading.Tasks", "System.Collections.Generic", "System.Text.RegularExpressions",
                    "Discord", "Discord.Rest", "Discord.Commands", "Discord.WebSocket",
                    "PacManBot", "PacManBot.Games", "PacManBot.Utils", "PacManBot.Commands", "PacManBot.Services", "PacManBot.Extensions"
                )
                .WithReferences(
                    typeof(ScriptingService).Assembly,
                    typeof(Discord.DiscordConfig).Assembly
                );
        }


        public async Task<object> EvalAsync(string code, object globals)
        {
            try
            {
                return await CSharpScript.EvaluateAsync(code, scriptOptions, globals);
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
