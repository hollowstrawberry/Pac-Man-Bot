using Microsoft.Data.Sqlite;

namespace PacManBot.Extensions
{
    public static class DatabaseExtensions
    {
        /// <summary>Fluent approach to adding a parameter and value to a <see cref="SqliteCommand"/>'s
        /// <see cref="SqliteParameterCollection"/>.</summary>
        public static SqliteCommand WithParameter(this SqliteCommand command, string name, object value)
        {
            command.Parameters.AddWithValue(name, value);
            return command;
        }
    }
}
