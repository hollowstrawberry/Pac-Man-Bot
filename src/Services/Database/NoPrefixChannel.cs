using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PacManBot.Services.Database
{
    [Table("NoPrefixChannels")]
    public class NoPrefixChannel
    {
        [Key] public ulong Id { get; set; }

        public NoPrefixChannel(ulong id)
        {
            Id = id;
        }


        public static implicit operator NoPrefixChannel(ulong id) => new NoPrefixChannel(id);
        public static implicit operator ulong(NoPrefixChannel c) => c.Id;

        public override bool Equals(object obj) => obj is NoPrefixChannel c && c.Id == Id;
        public override int GetHashCode() => Id.GetHashCode();
    }
}
