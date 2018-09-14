using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PacManBot.Services.Database
{
    [Table("NoPrefixChannels")]
    public class NoPrefixGuildChannel
    {
        [Key] public ulong Id { get; set; }

        public NoPrefixGuildChannel(ulong id)
        {
            Id = id;
        }


        public static implicit operator NoPrefixGuildChannel(ulong id) => new NoPrefixGuildChannel(id);
        public static implicit operator ulong(NoPrefixGuildChannel c) => c.Id;

        public override bool Equals(object obj) => obj is NoPrefixGuildChannel c && c.Id == Id;
        public override int GetHashCode() => Id.GetHashCode();
    }
}
