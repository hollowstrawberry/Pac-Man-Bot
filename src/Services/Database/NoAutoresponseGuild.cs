using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PacManBot.Services.Database
{
    [Table("NoAutoresponseGuilds")]
    public class NoAutoresponseGuild
    {
        [Key] public ulong Id { get; private set; }

        public NoAutoresponseGuild(ulong id)
        {
            Id = id;
        }


        public static implicit operator NoAutoresponseGuild(ulong id) => new NoAutoresponseGuild(id);
        public static implicit operator ulong(NoAutoresponseGuild g) => g.Id;

        public override bool Equals(object obj) => obj is NoAutoresponseGuild g && g.Id == Id;
        public override int GetHashCode() => Id.GetHashCode();
    }
}
