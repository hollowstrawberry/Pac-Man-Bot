using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Discord;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Games
{
    [DataContract]
    public class RubiksGame : ChannelGame, IUserGame, IStoreableGame
    {
        // Constants

        public override string Name => "Rubik's Cube";
        public override TimeSpan Expiry => TimeSpan.FromDays(7);
        public string FilenameKey => "rubik";


        private static readonly string[] ColorEmoji = {
            CustomEmoji.GreenSquare, CustomEmoji.WhiteSquare, CustomEmoji.RedSquare,
            CustomEmoji.OrangeSquare, CustomEmoji.YellowSquare, CustomEmoji.BlueSquare,
        };

        private static readonly IReadOnlyDictionary<string, string[]> ComplexMoves = new Dictionary<string, string[]> {
            { "M", new[] { "R", "L'", "X'", } },
            { "E", new[] { "U", "D'", "Y'", } },
            { "S", new[] { "F", "B'", "Z'", } },
            { "RW", new[] { "L", "X" } },
            { "UW", new[] { "D", "Y" } },
            { "FW", new[] { "B", "Z" } },
            { "LW", new[] { "R", "X'" } },
            { "DW", new[] { "U", "Y'" } },
            { "BW", new[] { "F", "Z'" } },

        }.AsReadOnly();


        // Fields

        private IReadOnlyDictionary<char, Face> allFaces;
        [DataMember] public bool showHelp = true;
        [DataMember] private Face front;
        [DataMember] private Face up;
        [DataMember] private Face right;
        [DataMember] private Face left;
        [DataMember] private Face down;
        [DataMember] private Face back;

        // Properties

        [DataMember] public override int Time { get; set; }
        [DataMember] public override DateTime LastPlayed { get; set; }
        [DataMember] public override ulong OwnerId { get => base.OwnerId; protected set => base.OwnerId = value; }
        [DataMember] public override ulong ChannelId { get => base.ChannelId; set => base.ChannelId = value; }
        [DataMember] public override ulong MessageId { get => base.MessageId; set => base.MessageId = value; }



        // Types

        private enum Sticker
        {
            Green,
            White,
            Red,
            Orange,
            Yellow,
            Blue,
        }


        private enum Edge // Start index of the 3 stickers connected at that edge
        {
            Up = 0,
            Right = 2,
            Down = 4,
            Left = 6,
        }



        [DataContract]
        private class Face
        {
            [DataMember] public Sticker center;
            [DataMember] public Sticker[] stickers;
            public IReadOnlyDictionary<Face, Edge> connections;

            private Face() { }

            public Face(Sticker color)
            {
                center = color;
                stickers = new Sticker[8];
                for (int i = 0; i < stickers.Length; i++) stickers[i] = color;
            }


            public void Turn(int amount)
            {
                amount = amount % 4; // Unnecessary turns
                if (Math.Abs(amount) == 3) amount /= -3; // 3 turns = 1 turn in opposite direction


                stickers.Shift(-2 * amount);

                var sideStickers = new Sticker[12];
                var adjacentFaces = connections.Keys.ToArray();

                for (int i = 0; i < adjacentFaces.Length; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        Face face = adjacentFaces[i];
                        int index = stickers.LoopedIndex((int)connections[face] + j);
                        sideStickers[i*3 + j] = face.stickers[index];
                    }
                }

                sideStickers.Shift(3 * amount);

                for (int i = 0; i < adjacentFaces.Length; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        Face face = adjacentFaces[i];
                        int index = stickers.LoopedIndex((int)connections[face] + j);
                        face.stickers[index] = sideStickers[i*3 + j];
                    }
                }
            }


            public string[] Rows()
            {
                var rows = new string[3];

                for (int i = 0; i < 3; i++) rows[0] += ColorEmoji[(int)stickers[i]];

                rows[1] += ColorEmoji[(int)stickers[7]];
                rows[1] += ColorEmoji[(int)center];
                rows[1] += ColorEmoji[(int)stickers[3]];

                for (int i = 6; i > 3; i--) rows[2] += ColorEmoji[(int)stickers[i]];

                return rows;
            }


            public override string ToString()
            {
                return string.Join('\n', Rows());
            }
        }




        private RubiksGame() { }

        public RubiksGame(ulong channelId, ulong ownerId, IServiceProvider services)
            : base(channelId, new[] { ownerId }, services)
        {
            front = new Face(Sticker.Green);
            up    = new Face(Sticker.White);
            right = new Face(Sticker.Red);
            left  = new Face(Sticker.Orange);
            down  = new Face(Sticker.Yellow);
            back  = new Face(Sticker.Blue);

            ConnectFaces();
        }

        
        public bool DoMoves(string input)
        {
            List<string> sequence = input.ToUpper().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (sequence.Any(x => !Regex.IsMatch(x, @"^([FURLDB]W?|[MESXYZ])'*[0-9]?'*$"))) return false;

            Time += sequence.Count;
            LastPlayed = DateTime.Now;

            for (int i = 0; i < sequence.Count; i++) // Replaces middle moves by their equivalent in other moves
            {
                string move = sequence[i];
                foreach (var replacement in ComplexMoves)
                {
                    if (move.StartsWith(replacement.Key))
                    {
                        sequence.RemoveAt(i);
                        foreach (string rep in replacement.Value)
                        {
                            sequence.Insert(i, move.Replace(replacement.Key, rep));
                        }
                    }
                }
            }

            foreach (string move in sequence)
            {
                var amountMatch = Regex.Match(move, @"[1-9]");
                if (!amountMatch.Success || !int.TryParse(amountMatch.Value, out int amount)) amount = 1;

                if (move.Count(x => x == '\'') % 2 == 1) amount *= -1; // Counterclockwise

                if (allFaces.ContainsKey(move[0])) allFaces[move[0]].Turn(amount);
                else RotateCube(move[0], amount);
            }

            storage.StoreGame(this);
            return true;
        }


        public void Scramble()
        {
            const int amount = 40;
            var faces = allFaces.Keys.ToList();
            var modifiers = new[] { "", "'", "2" };
            var moves = string.Join(" ", Enumerable.Range(0, amount).Select(x => Bot.Random.Choose(faces) + Bot.Random.Choose(modifiers)));

            Time = -amount; // Done before so it saves the game at 0
            if (!DoMoves(moves)) throw new Exception("Invalid generated shuffle sequence");
        }


        private void RotateCube(char letter, int amount)
        {
            amount = amount % 4;
            if (Math.Abs(amount) == 3) amount /= -3;
            if (amount == 0) return;

            Face[] axis;
            Face clockwise;
            Face counterClockwise;

            switch (letter)
            {
                case 'X':
                    axis = new[] { front, down, back, up };
                    back.stickers.Shift(4);
                    clockwise = left;
                    counterClockwise = right;
                    break;

                case 'Y':
                    axis = new[] { front, right, back, left };
                    clockwise = down;
                    counterClockwise = up;
                    break;

                case 'Z':
                    axis = new[] { up, left, down, right };
                    foreach (Face face in axis) face.stickers.Shift(-2 * amount);
                    clockwise = back;
                    counterClockwise = front;
                    break;

                default: throw new ArgumentException(nameof(letter));
            }

            var centers = axis.Select(f => f.center).ToArray();
            var stickers = axis.Select(f => f.stickers).ToArray();
            centers.Shift(amount);
            stickers.Shift(amount);
            for (int i = 0; i < axis.Length; i++)
            {
                axis[i].center = centers[i];
                axis[i].stickers = stickers[i];
            }

            if (letter == 'X') back.stickers.Shift(4);

            clockwise.stickers.Shift(2 * amount);
            counterClockwise.stickers.Shift(-2 * amount);
        }



        public override EmbedBuilder GetEmbed(bool _ = true) => GetEmbed(null);

        public EmbedBuilder GetEmbed(IGuild guild)
        {
            var description = new StringBuilder();

            string[] rowsFront = front.Rows();
            string[] rowsUp = up.Rows();
            string[] rowsRight = right.Rows();
            string[] rowsLeft = left.Rows();
            string[] rowsDown = down.Rows();
            string[] rowsBack = back.Rows();
            string emptyRow = CustomEmoji.Empty.Multiply(3);

            for (int i = 0; i < 3; i++)
            {
                description.Append($"{emptyRow} {rowsUp[i]}\n");
            }

            for (int i = 0; i < 3; i++)
            {
                description.Append($"{rowsLeft[i]} {rowsFront[i]} {rowsRight[i]} {rowsBack[i]}\n");
            }

            for (int i = 0; i < 3; i++)
            {
                description.Append($"{emptyRow} {rowsDown[i]}\n");
            }


            var embed = new EmbedBuilder
            {
                Title = $"{Owner?.Username}'s Rubik's Cube",
                Description = description.ToString(),
                Color = Colors.Black,
            };

            if (showHelp)
            {
                string prefix = storage.GetPrefixOrEmpty(guild);
                embed.AddField("Faces", $"```css\n  U\nL F R B\n  D```Do **{prefix}rubik moves** for help controlling the cube.");
            }

            return embed;
        }



        private void ConnectFaces()
        {
            front.connections = new Dictionary<Face, Edge> {
                {up, Edge.Down }, { left, Edge.Right }, { down, Edge.Up }, { right, Edge.Left },
            }.AsReadOnly();

            back.connections = new Dictionary<Face, Edge> {
                {up, Edge.Up }, { right, Edge.Right }, { down, Edge.Down }, { left, Edge.Left },
            }.AsReadOnly();

            up.connections = new Dictionary<Face, Edge> {
                {front, Edge.Up }, { right, Edge.Up }, { back, Edge.Up }, { left, Edge.Up },
            }.AsReadOnly();

            down.connections = new Dictionary<Face, Edge> {
                {front, Edge.Down }, { left, Edge.Down }, { back, Edge.Down }, { right, Edge.Down },
            }.AsReadOnly();

            right.connections = new Dictionary<Face, Edge> {
                {up, Edge.Right }, { front, Edge.Right }, { down, Edge.Right }, { back, Edge.Left },
            }.AsReadOnly();

            left.connections = new Dictionary<Face, Edge> {
                {up, Edge.Left }, { back, Edge.Right }, { down, Edge.Left }, { front, Edge.Left },
            }.AsReadOnly();


            allFaces = new Dictionary<char, Face>{
                { 'F', front }, { 'U', up }, { 'R', right },
                { 'L', left }, { 'D', down }, { 'B', back },
            }.AsReadOnly();
        }




        public void PostDeserialize(IServiceProvider services)
        {
            SetServices(services);

            ConnectFaces();
        }
    }
}
