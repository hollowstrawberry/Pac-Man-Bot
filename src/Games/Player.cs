using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Discord;
using PacManBot.Constants;

namespace PacManBot.Games
{
    /// <summary>
    /// Represents the index of the user in a <see cref="IMultiplayerGame"/>.
    /// Acts as a wrapper for <see cref="int"/>, like an enum.
    /// </summary>
    [DataContract, JsonConverter(typeof(PlayerJsonConverter))]
    public readonly struct Player
    {
        public static readonly Player
            None = -1,
            Tie = -2,

            Red = 0,
            Blue = 1,
            Green = 2,
            Yellow = 3,
            Purple = 4,
            Orange = 5;


        /// <summary>Standard player colors for multiplayer games.</summary>
        public static readonly Color[] AllColors = {
            Colors.Red, Colors.Blue, Colors.Green, Colors.Yellow, Colors.Purple, Colors.Orange,
        };

        /// <summary>Standard player color names for multiplayer games.</summary>
        public static readonly string[] ColorNames = {
            "Red", "Blue", "Green", "Yellow", "Purple", "Orange",
        };




        private readonly int value;

        private Player(int value)
        {
            this.value = value;
        }


        public static implicit operator int(Player player) => player.value;

        public static implicit operator Player(int value) => new Player(value);


        /// <summary>Returns the opposing player in a two-player game.</summary>
        public Player Opponent => value == Red ? Blue : Red;

        /// <summary>Returns the<see cref="Discord.Color"/> that represents this player.</summary>
        public Color Color
        {
            get
            {
                if (value >= 0 && value <= AllColors.Length) return AllColors[value];
                if (value == Tie) return Colors.Green;
                return Colors.Gray;
            }
        }


        /// <summary>Returns the name of the color that represents this player.</summary>
        public string ColorName
        {
            get
            {
                if (value >= 0 && value <= ColorNames.Length) return ColorNames[value];
                return "???";
            }
        }


        /// <summary>Returns this player's circle custom emoji.</summary>
        public string Circle(bool highlighted = false)
        {
            if (value == Red)  return highlighted ? CustomEmoji.C4redHL : CustomEmoji.C4red;
            if (value == Blue) return highlighted ? CustomEmoji.C4blueHL : CustomEmoji.C4blue;
            if (value == None) return CustomEmoji.BlackCircle;
            return CustomEmoji.Staff;
        }


        /// <summary>Returns this player's Tic-Tac-Toe symbol custom emoji.</summary>
        public string Symbol(bool highlighted = false)
        {
            if (value == Red) return highlighted ? CustomEmoji.TTTxHL : CustomEmoji.TTTx;
            if (value == Blue) return highlighted ? CustomEmoji.TTToHL : CustomEmoji.TTTo;
            if (value == None) return null;
            return CustomEmoji.Staff;
        }


        public override string ToString() => value.ToString();

        public override bool Equals(object obj) => obj is Player p ? p == this : obj is int i && i == value;

        public override int GetHashCode() => value.GetHashCode();




        /// <summary>Serializes and deserializes a <see cref="Player"/> as its underlying <see cref="int"/> value.</summary>
        public class PlayerJsonConverter : JsonConverter<Player>
        {
            public override Player ReadJson(JsonReader reader, Type objectType, Player existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                return new Player(int.Parse((string)JToken.ReadFrom(reader)));
            }

            public override void WriteJson(JsonWriter writer, Player value, JsonSerializer serializer)
            {
                writer.WriteValue(value.value);
            }
        }
    }
}
