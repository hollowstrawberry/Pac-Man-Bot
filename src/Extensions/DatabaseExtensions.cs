using Microsoft.Data.Sqlite;

namespace PacManBot.Extensions
{
    public static class DatabaseExtensions
    {
        public static SqliteCommand WithParameter(this SqliteCommand command, string name, object value)
        {
            command.Parameters.AddWithValue(name, value);
            return command;
        }
    }
}
