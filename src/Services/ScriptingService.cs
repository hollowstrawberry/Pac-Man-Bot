using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace PacManBot.Services
{
    /// <summary>
    /// Executes code and returns the result using predefined options, always garbage-collecting afterwards.
    /// Thank you to oatmeal and amibu.
    /// </summary>
    public class ScriptingService
    {
        private readonly ScriptOptions scriptOptions;


        public ScriptingService()
        {
            scriptOptions = ScriptOptions.Default
                .WithImports( // Just give me everything
                    "System", "System.IO", "System.Text", "System.Linq", "System.Reflection", "System.Diagnostics",
                    "System.Threading.Tasks", "System.Collections.Generic", "System.Text.RegularExpressions",
                    "Microsoft.EntityFrameworkCore", "Newtonsoft.Json",
                    "Discord", "Discord.Rest", "Discord.Commands", "Discord.WebSocket",
                    "PacManBot", "PacManBot.Constants", "PacManBot.Utils", "PacManBot.Extensions", 
                    "PacManBot.Games", "PacManBot.Games.Concrete", "PacManBot.Games.Concrete.Rpg",
                    "PacManBot.Commands", "PacManBot.Commands.Modules", "PacManBot.Commands.Modules.GameModules",
                    "PacManBot.Services", "PacManBot.Services.Database"
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
