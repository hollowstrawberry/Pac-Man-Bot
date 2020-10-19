using System.Threading.Tasks;
using PacManBot.Services;

namespace PacManBot.Extensions
{
    public static class TaskExtensions
    {
        /// <summary>Continues a task with an action to log exceptions if any were thrown.</summary>
        public static Task LogExceptions(this Task task, LoggingService log, string sourceMessage)
        {
            return task.ContinueWith(x => log.Exception(sourceMessage, x.Exception), TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
