using Microsoft.EntityFrameworkCore;

namespace PacManBot.Services.Database
{
    public class PacManDbContext : DbContext
    {
        public DbSet<ScoreEntry> PacManScores { get; private set; }
        public DbSet<GuildPrefix> Prefixes { get; private set; }
        public DbSet<NoPrefixGuildChannel> NoPrefixGuildChannels { get; private set; }
        public DbSet<NoAutoresponseGuild> NoAutoresponseGuilds { get; private set; }

        private readonly string connectionString;


        public PacManDbContext(string connectionString) : base()
        {
            this.connectionString = connectionString;
        }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(connectionString);
        }
    }
}
