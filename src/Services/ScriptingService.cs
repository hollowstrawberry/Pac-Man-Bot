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
        private readonly ScriptOptions _scriptOptions;


        public ScriptingService()
        {
            _scriptOptions = ScriptOptions.Default
                .WithImports( // Just give me everything
                    "System", "System.IO", "System.Text", "System.Linq", "System.Reflection", "System.Diagnostics",
                    "System.Threading.Tasks", "System.Collections.Generic", "System.Text.RegularExpressions",
                    "Microsoft.EntityFrameworkCore", "Newtonsoft.Json",
                    "DSharpPlus", "DSharpPlus.CommandsNext", "DSharpPlus.Entities", "DSharpPlus.Exceptions",
                    "PacManBot", "PacManBot.Constants", "PacManBot.Utils", "PacManBot.Extensions", 
                    "PacManBot.Games", "PacManBot.Games.Concrete", "PacManBot.Games.Concrete.Rpg",
                    "PacManBot.Commands", "PacManBot.Commands.Modules",
                    "PacManBot.Services", "PacManBot.Services.Database"
                )
                .WithReferences(
                    typeof(PmBot).Assembly,
                    typeof(DSharpPlus.DiscordClient).Assembly
                );
        }


        public async Task<object> EvalAsync(string code, object globals)
        {
            try
            {
                return await CSharpScript.EvaluateAsync(code, _scriptOptions, globals);
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
