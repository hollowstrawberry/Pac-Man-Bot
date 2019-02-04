using System.Runtime.Serialization;
using PacManBot.Games;
using PacManBot.Games.Concrete;

namespace PacManBot
{
    /// <summary>
    /// Contains data used throughout the bot, loaded from a file.
    /// </summary>
    [DataContract]
    public class PmContent
    {
        /// <summary>Short invite link for the bot.</summary>
        [DataMember] public readonly string inviteLink;

        /// <summary>GitHub repository link for the bot.</summary>
        [DataMember] public readonly string githubLink;

        /// <summary>Invite link to the bot's support server.</summary>
        [DataMember] public readonly string serverLink;

        /// <summary>Message in the about command.</summary>
        [DataMember] public readonly string about;

        /// <summary>Fields in the about command.</summary>
        [DataMember] public readonly (string name, string desc)[] aboutFields;




        /// <summary><see cref="PacManGame"/> map.</summary>
        [DataMember] public readonly string gameMap;

        /// <summary><see cref="PacManGame"/> in-game help.</summary>
        [DataMember] public readonly string gameHelp;

        /// <summary><see cref="PacManGame"/> custom map creation help.</summary>
        [DataMember] public readonly string customHelp;

        /// <summary><see cref="PacManGame"/> custom map creation help links.</summary>
        [DataMember] public readonly (string name, string url)[] customLinks;



        /// <summary>AI match flavor text for <see cref="MultiplayerGame"/>.</summary>
        [DataMember] public readonly string[] gameStartTexts;

        /// <summary>AI match flavor text for <see cref="MultiplayerGame"/>.</summary>
        [DataMember] public readonly string[] gamePlayingTexts;

        /// <summary>AI match flavor text for <see cref="MultiplayerGame"/>.</summary>
        [DataMember] public readonly string[] gameWinTexts;

        /// <summary>AI match flavor text for <see cref="MultiplayerGame"/>.</summary>
        [DataMember] public readonly string[] gameNotWinTexts;



        /// <summary><see cref="PetGame"/> default pet image.</summary>
        [DataMember] public readonly string petImageUrl;

        /// <summary>Food emoji used as reactions for <see cref="PetGame"/>.</summary>
        [DataMember] public readonly string[] petFoodEmotes;

        /// <summary>Game emoji used as reactions for <see cref="PetGame"/>.</summary>
        [DataMember] public readonly string[] petPlayEmotes;

        /// <summary>Cleaning emoji used as reactions for <see cref="PetGame"/>.</summary>
        [DataMember] public readonly string[] petCleanEmotes;

        /// <summary>Sleeping emoji used as reactions for <see cref="PetGame"/>.</summary>
        [DataMember] public readonly string[] petSleepEmotes;

        /// <summary>Banners as rewards in <see cref="PetGame"/>.</summary>
        [DataMember] public readonly string[] petBannerUrl;

        /// <summary>Messages used in <see cref="PetGame"/> petting.</summary>
        [DataMember] public readonly string[] pettingMessages;

        /// <summary>Messages used in <see cref="PetGame"/> petting.</summary>
        [DataMember] public readonly string[] superPettingMessages;



        /// <summary>Image URLs for <see cref="HangmanGame"/> mistake stages.</summary>
        [DataMember] public readonly string[] hangmanStageImages;

        /// <summary>Words used in <see cref="HangmanGame"/>.</summary>
        [DataMember] public readonly string[] hangmanWords;
    }
}
