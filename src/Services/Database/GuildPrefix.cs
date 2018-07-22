using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PacManBot.Services.Database
{
    [Table("Prefixes")]
    public class GuildPrefix
    {
        [Key] public ulong Id { get; private set; }
        public string Prefix { get; set; }

        public GuildPrefix(ulong id, string prefix)
        {
            Id = id;
            Prefix = prefix;
        }

        public static implicit operator GuildPrefix((ulong id, string prefix) tuple) => new GuildPrefix(tuple.id, tuple.prefix);

        public override bool Equals(object obj) => obj is GuildPrefix prefix && prefix.Id == Id;
        public override int GetHashCode() => Id.GetHashCode();
    }
}
