using System.Data.SQLite;

namespace PacManBot.Extensions
{
    public static class DatabaseExtensions
    {
        public static SQLiteCommand WithParameter(this SQLiteCommand command, string name, object value)
        {
            command.Parameters.AddWithValue(name, value);
            return command;
        }
    }
}
