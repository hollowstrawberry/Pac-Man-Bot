using System.Runtime.Serialization;

namespace PacManBot
{
    /// <summary>
    /// Contains content used throughout the bot, loaded from a file.
    /// </summary>
    [DataContract]
    public class BotContent
    {
        /// <summary>Displayed version number of the bot.</summary>
        [DataMember] public readonly string version;

        /// <summary>Short invite link for the bot.</summary>
        [DataMember] public readonly string invite;

        /// <summary>Message in the about command.</summary>
        [DataMember] public readonly string about;

        /// <summary>Fields in the about command.</summary>
        [DataMember] public readonly (string name, string desc)[] aboutFields;

        /// <summary><see cref="Games.Concrete.PacManGame"/> map.</summary>
        [DataMember] public readonly string gameMap;

        /// <summary><see cref="Games.Concrete.PacManGame"/> in-game help.</summary>
        [DataMember] public readonly string gameHelp;

        /// <summary><see cref="Games.Concrete.PacManGame"/> custom map creation help.</summary>
        [DataMember] public readonly string customHelp;

        /// <summary><see cref="Games.Concrete.PacManGame"/> custom map creation help links.</summary>
        [DataMember] public readonly (string name, string url)[] customLinks;

        /// <summary><see cref="Games.Concrete.PetGame"/> default pet image.</summary>
        [DataMember] public readonly string petImageUrl;

        /// <summary><see cref="Games.Concrete.PetGame"/> messages.</summary>
        [DataMember] public readonly string[] pettingMessages;

        /// <summary><see cref="Games.Concrete.PetGame"/> advanced messages.</summary>
        [DataMember] public readonly string[] superPettingMessages;
    }
}
