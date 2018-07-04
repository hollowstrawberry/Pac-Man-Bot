using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Discord;
using Discord.WebSocket;
using PacManBot.Services;
using PacManBot.Constants;
using PacManBot.Extensions;

namespace PacManBot.Games
{
    public class InvalidMapException : Exception
    {
        public InvalidMapException() { }
        public InvalidMapException(string message) : base(message) { }
        public InvalidMapException(string message, Exception inner) : base(message, inner) { }
    }


    [DataContract] // Serializable to store in JSON
    public class PacManGame : ChannelGame, IReactionsGame, IStoreableGame
    {
        // Constants

        public override string Name => "Pac-Man";
        public override TimeSpan Expiry => TimeSpan.FromDays(7);
        public string FilenameKey => "";


        public static readonly IReadOnlyDictionary<IEmote, PacManInput> GameInputs = new Dictionary<IEmote, PacManInput>() {
            { "⬅".ToEmoji(), PacManInput.Left },
            { "⬆".ToEmoji(), PacManInput.Up },
            { "⬇".ToEmoji(), PacManInput.Down },
            { "➡".ToEmoji(), PacManInput.Right },
            { CustomEmoji.EHelp, PacManInput.Help },
            { "⏭".ToEmoji(), PacManInput.Fast }
        }.AsReadOnly();

        private const int PowerTime = 20, ScatterCycle = 100, ScatterTime1 = 30, ScatterTime2 = 20;
        private const char CharPlayer = 'O', CharFruit = '$', CharGhost = 'G', CharSoftWall = '_', CharSoftWallPellet = '~'; // Read from map
        private const char CharDoor = '-', CharPellet = '·', CharPowerPellet = '●', CharPlayerDead = 'X', CharGhostFrightened = 'E'; // Displayed
        private static readonly char[] GhostAppearance = { 'B', 'P', 'I', 'C' };
        private static readonly int[] GhostSpawnPauseTime = { 0, 3, 15, 35 };
        private static readonly Dir[] AllDirs = { Dir.Up, Dir.Left, Dir.Down, Dir.Right }; // Order of preference when deciding direction


        // Fields

        [DataMember] public bool custom;
        [DataMember] public bool mobileDisplay;
        [DataMember] public int score;

        private char[,] map;
        [DataMember] private readonly int maxPellets;
        [DataMember] private int pellets;
        [DataMember] private int oldScore;
        [DataMember] private PacMan pacMan;
        [DataMember] private List<Ghost> ghosts;
        [DataMember] private int fruitTimer;
        [DataMember] private Pos fruitSpawnPos; // Where all fruit will spawn
        [DataMember] private PacManInput lastInput = PacManInput.None;
        [DataMember] private bool fastForward = true;


        // Properties

        [DataMember] public override State State { get; set; }
        [DataMember] public override DateTime LastPlayed { get; set; }
        [DataMember] public override int Time { get; set; }
        [DataMember] public override ulong MessageId { get; set; }
        [DataMember] public override ulong ChannelId { get; set; }
        [DataMember] public override ulong OwnerId { get => base.OwnerId; protected set => base.OwnerId = value; }

        private Pos FruitSecondPos => fruitSpawnPos + Dir.Right; // Second tile which fruit will also occupy
        private int FruitTrigger1 => maxPellets - 70; // Amount of pellets remaining needed to spawn fruit
        private int FruitTrigger2 => maxPellets - 170;
        private int FruitScore => (pellets > FruitTrigger2) ? 1000 : 2000;
        private char FruitChar => (pellets > FruitTrigger2) ? 'x' : 'w';


        [DataMember]
        private string FullMap // Converts map between char[,] and string
        {
            set
            {
                if (value.Length > 1500)
                {
                    throw new InvalidMapException("Map exceeds the maximum length of 1500 characters");
                }
                if (!value.ContainsAny(' ', CharSoftWall, CharPellet, CharPowerPellet, CharSoftWallPellet))
                {
                    throw new InvalidMapException("Map is completely solid");
                }

                string[] lines = value.Split('\n');
                int width = lines[0].Length, height = lines.Length;
                map = new char[width, height];

                for (int y = 0; y < height; y++)
                {
                    if (lines[y].Length != width) throw new InvalidMapException("Map width not constant");
                    for (int x = 0; x < width; x++)
                    {
                        map[x, y] = lines[y][x];
                    }
                }
            }

            get
            {
                var stringMap = new StringBuilder();
                for (int y = 0; y < map.Y(); y++)
                {
                    if (y > 0) stringMap.Append('\n');
                    for (int x = 0; x < map.X(); x++)
                    {
                        stringMap.Append(map[x, y]);
                    }
                }
                return stringMap.ToString();
            }
        }




        // Types

        public enum PacManInput
        {
            None,
            Up,
            Left,
            Down,
            Right,
            Wait,
            Help,
            Fast,
        }


        private enum GhostType
        {
            Blinky,
            Pinky,
            Inky,
            Clyde
        }


        private enum GhostMode
        {
            Chase,
            Scatter,
            Frightened
        }



        [DataContract]
        private class PacMan
        {
            [DataMember] public readonly Pos origin; // Position it started at
            [DataMember] public Pos pos; // Position on the map
            [DataMember] public Dir dir = Dir.None; // Direction it's facing
            [DataMember] public int power; // Time left of power mode
            [DataMember] public int ghostStreak; // Ghosts eaten during the current power mode


            private PacMan() { }

            public PacMan(Pos pos)
            {
                this.pos = pos;
                origin = pos;
            }
        }


        [DataContract]
        private class Ghost
        {
            [DataMember] public readonly Pos origin; // Tile it spawns in
            [DataMember] public readonly Pos corner; // Preferred corner
            [DataMember] public Pos pos; // Position on the map
            [DataMember] public Dir dir; // Direction it's facing
            [DataMember] public GhostType type; // Ghost behavior type
            [DataMember] public GhostMode mode; // Ghost behavior mode
            [DataMember] public int pauseTime; // Time remaining until it can move
            [DataMember] public bool exitRight; // It will exit the ghost box to the left unless modes have changed

            public char Appearance => mode == GhostMode.Frightened ? CharGhostFrightened : GhostAppearance[(int)type];


            private Ghost() { }

            public Ghost(GhostType type, Pos pos, Pos? corner)
            {
                this.type = type;
                this.pos = pos;
                this.corner = corner ?? pos;
                origin = pos;
                pauseTime = GhostSpawnPauseTime[(int)type];
            }
        }




        // Game methods


        private PacManGame() { } // Used by JSON deserializing

        public PacManGame(ulong channelId, ulong ownerId, string newMap, bool mobileDisplay,
                          DiscordShardedClient client, LoggingService logger, StorageService storage)
            : base(channelId, new[] { ownerId }, client, logger, storage)
        {
            this.mobileDisplay = mobileDisplay;

            // Map
            if (newMap == null) newMap = storage.BotContent["map"];
            else custom = true;

            FullMap = newMap; // Converts string into char[,]

            maxPellets = newMap.Count(c => c == CharPellet || c == CharPowerPellet || c == CharSoftWallPellet);
            pellets = maxPellets;

            // Game objects
            Pos playerPos = FindChar(CharPlayer).GetValueOrDefault();
            pacMan = new PacMan(playerPos);
            map.SetAt(pacMan.pos, ' ');

            fruitSpawnPos = FindChar(CharFruit) ?? new Pos(-1, -1);
            if (fruitSpawnPos.x >= 0) map.SetAt(fruitSpawnPos, ' ');

            ghosts = new List<Ghost>();
            Pos[] ghostCorners = { // Matches original game
                new Pos(map.X() - 3, -3),
                new Pos(2, -3),
                new Pos(map.X() - 1, map.Y()),
                new Pos(0, map.Y())
            };
            for (int i = 0; i < 4; i++)
            {
                Pos? ghostPos = FindChar(CharGhost);
                if (!ghostPos.HasValue) break;
                ghosts.Add(new Ghost((GhostType)i, ghostPos.Value, ghostCorners[i]));
                map.SetAt(ghostPos.Value, ' ');
            }

            storage.StoreGame(this);
            if (custom) File.AppendAllText(Files.CustomMapLog, newMap);
        }




        public bool IsInput(IEmote emote, ulong userId = 1)
        {
            return GameInputs.ContainsKey(emote);
        }


        public void Input(IEmote emote, ulong userId = 1)
        {
            if (State != State.Active) return;
            LastPlayed = DateTime.Now;

            PacManInput input = GameInputs[emote];

            if (lastInput == PacManInput.Help) // Closes help
            {
                lastInput = PacManInput.None;
                return;
            }

            lastInput = input;

            if (input == PacManInput.Help) return; // Opens help
            if (input == PacManInput.Fast) // Toggle fastforward
            {
                fastForward = !fastForward;
                return;
            }

            oldScore = score;
            bool continueInput = true;

            Dir newDir = Dir.None;
            switch (input)
            {
                case PacManInput.Up:    newDir = Dir.Up; break;
                case PacManInput.Right: newDir = Dir.Right; break;
                case PacManInput.Down:  newDir = Dir.Down; break;
                case PacManInput.Left:  newDir = Dir.Left; break;
            }


            int consecutive = 0;

            do
            {
                Time++;
                consecutive++;

                // Player
                if (newDir != Dir.None)
                {
                    pacMan.dir = newDir;
                    if (NonSolid(pacMan.pos + newDir)) pacMan.pos += newDir;
                    map.Wrap(ref pacMan.pos);
                }

                foreach (Dir dir in AllDirs) // Check perpendicular directions
                {
                    int diff = Math.Abs((int)pacMan.dir - (int)dir);
                    if ((diff == 1 || diff == 3) && NonSolid(pacMan.pos + dir)) continueInput = false; // Stops at intersections
                }

                // Fruit
                if (fruitTimer > 0)
                {
                    if (fruitSpawnPos == pacMan.pos || FruitSecondPos == pacMan.pos)
                    {
                        score += FruitScore;
                        fruitTimer = 0;
                        continueInput = false;
                    }
                    else
                    {
                        fruitTimer--;
                    }
                }

                // Pellet collision
                char tile = map.At(pacMan.pos);
                if (tile == CharPellet || tile == CharPowerPellet || tile == CharSoftWallPellet)
                {
                    pellets--;
                    if ((pellets == FruitTrigger1 || pellets == FruitTrigger2) && fruitSpawnPos.x >= 0)
                    {
                        fruitTimer = Bot.Random.Next(25, 30 + 1);
                    }

                    score += (tile == CharPowerPellet) ? 50 : 10;
                    map[pacMan.pos.x, pacMan.pos.y] = (tile == CharSoftWallPellet) ? CharSoftWall : ' ';
                    if (tile == CharPowerPellet)
                    {
                        pacMan.power += PowerTime;
                        foreach (Ghost ghost in ghosts) ghost.mode = GhostMode.Frightened;
                        continueInput = false;
                    }

                    if (pellets == 0)
                    {
                        State = State.Win;
                    }
                }

                // Ghosts
                foreach (Ghost ghost in ghosts)
                {
                    bool didAi = false;
                    while (true) // Checks player collision before and after AI
                    {
                        if (pacMan.pos == ghost.pos) // Collision
                        {
                            if (ghost.mode == GhostMode.Frightened)
                            {
                                ghost.pauseTime = 6;
                                ghost.mode = CurrentGhostMode(); // Removes frightened State
                                score += 200 * (int)Math.Pow(2, pacMan.ghostStreak); // Each ghost gives double the points of the last
                                pacMan.ghostStreak++;
                            }
                            else State = State.Lose;

                            continueInput = false;
                            didAi = true;
                        }

                        if (didAi || State != State.Active) break; // Doesn't run AI twice, or if the user already won

                        GhostAi(ghost); // Full ghost behavior
                        didAi = true;
                    }
                }

                if (pacMan.power > 0) pacMan.power--;
                if (pacMan.power == 0) pacMan.ghostStreak = 0;

            } while (fastForward && continueInput && consecutive <= 20);


            if (State == State.Active)
            {
                storage.StoreGame(this);
            }
        }


        public override string GetContent(bool showHelp = true)
        {
            if (State == State.Cancelled && Channel is IGuildChannel) // So as to not spam
            {
                return "";
            }

            if (lastInput == PacManInput.Help)
            {
                return storage.BotContent["gamehelp"].Replace("{prefix}", storage.GetPrefixOrEmpty(Guild));
            }

            try
            {
                var display = new StringBuilder(); // The final display in string form
                var displayMap = (char[,])map.Clone(); // The display array to modify

                // Scan replacements
                for (int y = 0; y < map.Y(); y++)
                {
                    for (int x = 0; x < map.X(); x++)
                    {
                        if (displayMap[x, y] == CharSoftWall) displayMap[x, y] = ' ';
                        else if (displayMap[x, y] == CharSoftWallPellet) displayMap[x, y] = CharPellet;

                        if (mobileDisplay) // Mode with simplified characters
                        {
                            if (!NonSolid(x, y) && displayMap[x, y] != CharDoor) displayMap[x, y] = '#'; // Walls
                            else if (displayMap[x, y] == CharPellet) displayMap[x, y] = '.'; // Pellets
                            else if (displayMap[x, y] == CharPowerPellet) displayMap[x, y] = 'o'; // Power pellets
                        }
                    }
                }

                // Adds fruit, ghosts and player
                if (fruitTimer > 0)
                {
                    displayMap[fruitSpawnPos.x, fruitSpawnPos.y] = FruitChar;
                    displayMap[FruitSecondPos.x, FruitSecondPos.y] = FruitChar;
                }
                foreach (Ghost ghost in ghosts)
                {
                    displayMap[ghost.pos.x, ghost.pos.y] = ghost.Appearance;
                }
                displayMap[pacMan.pos.x, pacMan.pos.y] = State == State.Lose ? CharPlayerDead : CharPlayer;

                // Converts 2d array to string
                for (int y = 0; y < displayMap.Y(); y++)
                {
                    for (int x = 0; x < displayMap.X(); x++)
                    {
                        display.Append(displayMap[x, y]);
                    }
                    display.Append('\n');
                }

                // Add text to the side
                string[] info = {
                    $"┌{"───< Mobile Mode >───┐".If(mobileDisplay)}",
                    $"│ {"#".If(!mobileDisplay)}Time: {Time}",
                    $"│ {"#".If(!mobileDisplay)}Score: {score}{$" +{score - oldScore}".If(score - oldScore != 0)}",
                    $"│ {$"{"#".If(!mobileDisplay)}Power: {pacMan.power}".If(pacMan.power > 0)}",
                    $"│ ",
                    $"│ {CharPlayer} - Pac-Man{$": {pacMan.dir.ToString().ToLower()}".If(pacMan.dir != Dir.None)}",
                    $"│ ",
                    $"│ ", " │ ", " │ ", " │ ", // 7-10: ghosts
                    $"│ ",
                    $"│ {($"{FruitChar}{FruitChar} - Fruit: {fruitTimer}").If(fruitTimer > 0)}",
                    $"└"
                };

                for (int i = 0; i < ghosts.Count; i++)
                {
                    info[i + 7] = $"│ {ghosts[i].Appearance} - {(GhostType)i}"
                                + $": {ghosts[i].dir.ToString().ToLower()}".If(ghosts[i].dir != Dir.None);
                }

                if (mobileDisplay)
                {
                    display.Insert(0, string.Join('\n', info) + "\n\n");
                }
                else
                {
                    for (int i = 0; i < info.Length && i < map.Y(); i++) // Insert info
                    {
                        int insertIndex = (i + 1) * displayMap.X(); // Skips ahead a certain amount of lines
                        for (int j = i - 1; j >= 0; j--) insertIndex += info[j].Length + 2; // Takes into account the added line length of previous info
                        display.Insert(insertIndex, $" {info[i]}");
                    }
                }

                // Code tags
                switch (State)
                {
                    case State.Lose:
                        display.Insert(0, "```diff\n");
                        display.Replace("\n", "\n-", 0, display.Length - 1); // All red
                        break;

                    case State.Win:
                        display.Insert(0, "```diff\n");
                        display.Replace("\n", "\n+", 0, display.Length - 1); // All green
                        break;

                    case State.Cancelled:
                        display.Insert(0, "```diff\n");
                        display.Replace("\n", "\n*** ", 0, display.Length - 1); // All gray
                        break;

                    default:
                        display.Insert(0, mobileDisplay ? "```\n" : "```css\n");
                        display.Append($"#Fastforward: {(fastForward ? "Active" : "Disabled")}");
                        break;
                }
                display.Append("```");

                if (State != State.Active || custom) // Secondary box
                {
                    display.Append("```diff");
                    switch (State)
                    {
                        case State.Win: display.Append("\n+You won!"); break;
                        case State.Lose: display.Append("\n-You lost!"); break;
                        case State.Cancelled: display.Append($"\n-Game has been ended.{" Score not saved".If(!custom)}"); break;
                    }
                    if (custom) display.Append("\n*** Custom game: Score won't be registered. ***");
                    display.Append("```");
                }

                if (showHelp && State == State.Active && Time < 5)
                {
                    display.Append($"\n(Confused? React with {CustomEmoji.Help} for help)");
                }


                return display.ToString();
            }
            catch (Exception e)
            {
                logger.Log(LogSeverity.Error, LogSource.Game, $"{e}").GetAwaiter().GetResult();
                return $"```There was an error displaying the game. {"Make sure your custom map is valid. ".If(custom)}" +
                       $"If this problem persists, please contact the author of the bot using the " +
                       $"{storage.GetPrefixOrEmpty(Guild)}feedback command.```";
            }
        }


        public override EmbedBuilder GetEmbed(bool showHelp = true)
        {
            if (State == State.Cancelled && Channel is IGuildChannel) return CancelledEmbed();
            return null;
        }




        private void GhostAi(Ghost ghost)
        {
            // Decide mode
            if (pacMan.power <= 1) ghost.mode = CurrentGhostMode();

            if (ghost.pauseTime > 0) // In the cage
            {
                ghost.pos = ghost.origin;
                ghost.dir = Dir.None;
                ghost.pauseTime--;
                if (JustChangedGhostMode(ghost, fullCheck: true)) ghost.exitRight = true;
                return;
            }

            if (ghost.mode == GhostMode.Frightened && Time % 2 == 1) // If frightened, only moves on even turns
            {
                if (JustChangedGhostMode(ghost)) ghost.dir = ghost.dir.Opposite();
                return;
            }

            // Decide tile it's trying to reach
            Pos target = default;

            switch (ghost.mode)
            {
                case GhostMode.Chase: // Normal
                    switch (ghost.type)
                    {
                        case GhostType.Blinky:
                            target = pacMan.pos;
                            break;

                        case GhostType.Pinky:
                            target = pacMan.pos + pacMan.dir.ToPos(4);
                            if (pacMan.dir == Dir.Up) target += Dir.Left.ToPos(4); // Intentional bug from the original arcade
                            break;

                        case GhostType.Inky:
                            target = pacMan.pos + pacMan.dir.ToPos(2);
                            if (pacMan.dir == Dir.Up) target += Dir.Left.ToPos(2); // Intentional bug from the original arcade
                            target += target - ghosts[(int)GhostType.Blinky].pos; // Opposite position relative to Blinky
                            break;

                        case GhostType.Clyde:
                            target = Pos.Distance(ghost.pos, pacMan.pos) > 8 ? pacMan.pos : ghost.corner; // Gets scared if too close
                            break;
                    }
                    break;

                case GhostMode.Scatter:
                    target = ghost.corner;
                    break;
            }


            // Decide movement

            if (map.At(ghost.pos) == CharDoor || map.At(ghost.pos + Dir.Up) == CharDoor) // Exiting the cage
            {
                ghost.dir = Dir.Up;
            }
            else if (ghost.dir == Dir.Up && map.At(ghost.pos + Dir.Down) == CharDoor) // Getting away from the cage
            {
                ghost.dir = ghost.exitRight ? Dir.Right : Dir.Left;
            }
            else if (JustChangedGhostMode(ghost))
            {
                ghost.dir = ghost.dir.Opposite();
            }
            else if (ghost.mode == GhostMode.Frightened) // Turns randomly at intersections
            {
                ghost.dir = Bot.Random.Choose(AllDirs.Where(x => x != ghost.dir.Opposite() && NonSolid(ghost.pos + x)).ToArray());
            }
            else // Track target
            {
                ghost.exitRight = false;

                float distance = float.PositiveInfinity;
                foreach (Dir testDir in AllDirs
                    .Where(x => x != ghost.dir.Opposite())
                    .ToArray())
                {
                    Pos testPos = ghost.pos + testDir;

                    if (testDir == Dir.Up && (map.At(testPos) == CharSoftWall || map.At(testPos) == CharSoftWallPellet)) continue;

                    if (NonSolid(testPos) && Pos.Distance(testPos, target) < distance)
                    {
                        ghost.dir = testDir;
                        distance = Pos.Distance(testPos, target);
                    }
                    // Console.WriteLine($"{type}: {pos} / Target: {target} / Test Dir: {testDir} / Test Dist: {Pos.Distance(pos + testDir, target)}");
                }
            }

            ghost.pos += ghost.dir;
            map.Wrap(ref ghost.pos);
        }


        private GhostMode CurrentGhostMode()
        {
            return Time < 4 * ScatterCycle  // In set cycles, a set number of turns is spent in scatter mode, up to 4 times
                    && (Time < 2 * ScatterCycle && Time % ScatterCycle < ScatterTime1
                    || Time >= 2 * ScatterCycle && Time % ScatterCycle < ScatterTime2)
                ? GhostMode.Scatter
                : GhostMode.Chase;
        }


        private bool JustChangedGhostMode(Ghost ghost, bool fullCheck = false) // fullCheck detects changes to other ghosts
        {
            if (Time == 0) return false;
            if (ghost.mode == GhostMode.Frightened && !fullCheck) return pacMan.power == PowerTime; // Detects the switch to Frightened, but not from it

            for (int i = 0; i < 2; i++) if (Time == i * ScatterCycle || Time == i * ScatterCycle + ScatterTime1) return true;
            for (int i = 2; i < 4; i++) if (Time == i * ScatterCycle || Time == i * ScatterCycle + ScatterTime2) return true;

            return fullCheck && pacMan.power == PowerTime;
        }




        private Pos? FindChar(char c, int index = 0) // Finds the specified character instance in the map
        {
            for (int y = 0; y < map.Y(); y++)
            {
                for (int x = 0; x < map.X(); x++)
                {
                    if (map[x, y] == c)
                    {
                        if (index > 0) index--;
                        else
                        {
                            return new Pos(x, y);
                        }
                    }
                }
            }

            return null;
        }


        private bool NonSolid(int x, int y) => NonSolid(new Pos(x, y));
        private bool NonSolid(Pos pos) // Defines which tiles in the map entities can move through
        {
            char ch = map.At(pos);
            return (ch == ' ' || ch == CharPellet || ch == CharPowerPellet || ch == CharSoftWall || ch == CharSoftWallPellet);
        }


        public void PostDeserialize(DiscordShardedClient client, LoggingService logger, StorageService storage)
        {
            this.client = client;
            this.logger = logger;
            this.storage = storage;
        }
    }
}
