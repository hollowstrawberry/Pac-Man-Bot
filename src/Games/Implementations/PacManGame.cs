using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Discord;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Services;
using static PacManBot.Games.GameUtils;

namespace PacManBot.Games
{
    public class InvalidMapException : Exception
    {
        public InvalidMapException() : base() { }
        public InvalidMapException(string message) : base(message) { }
        public InvalidMapException(string message, Exception inner) : base(message, inner) { }
    }


    [DataContract] // Serializable to store in JSON
    public class PacManGame : ChannelGame, IReactionsGame, IStoreableGame
    {
        // Constants

        public static readonly Dictionary<IEmote, GameInput> GameInputs = new Dictionary<IEmote, GameInput>() // Reaction controls
        {
            { "⬅".ToEmoji(), GameInput.Left },
            { "⬆".ToEmoji(), GameInput.Up },
            { "⬇".ToEmoji(), GameInput.Down },
            { "➡".ToEmoji(), GameInput.Right },
            { CustomEmoji.EHelp, GameInput.Help },
            { "⏭".ToEmoji(), GameInput.Fast }
        };

        private static readonly TimeSpan _expiry = TimeSpan.FromDays(7);

        private const int PowerTime = 20, ScatterCycle = 100, ScatterTime1 = 30, ScatterTime2 = 20;

        private const char CharPlayer = 'O', CharFruit = '$', CharGhost = 'G', CharSoftWall = '_', CharSoftWallPellet = '~'; // Read from map
        private const char CharDoor = '-', CharPellet = '·', CharPowerPellet = '●', CharPlayerDead = 'X', CharGhostFrightened = 'E'; // Displayed

        private readonly static char[] GhostAppearance = { 'B', 'P', 'I', 'C' }; // Matches aiType enum
        private readonly static int[] GhostSpawnPauseTime = { 0, 3, 15, 35 };
        private readonly static Dir[] AllDirs = { Dir.up, Dir.left, Dir.down, Dir.right }; // Order of preference when deciding direction


        // Fields

        [DataMember] public bool custom = false;
        [DataMember] public bool mobileDisplay = false;
        [DataMember] public int score = 0;

        [IgnoreDataMember] private char[,] map;
        [DataMember] private readonly int maxPellets;
        [DataMember] private int pellets; //Pellets remaining
        [DataMember] private int oldScore = 0; //Score obtained last turn
        [DataMember] private PacMan pacMan;
        [DataMember] private List<Ghost> ghosts;
        [DataMember] private int fruitTimer = 0;
        [DataMember] private Pos fruitSpawnPos; //Where all fruit will spawn
        [DataMember] private GameInput lastInput = GameInput.None;
        [DataMember] private bool fastForward = true;


        // Properties

        public override string Name => "Pac-Man";
        public override TimeSpan Expiry => _expiry;
        public string FilenameKey => "";

        [DataMember] public override State State { get; set; }
        [DataMember] public override DateTime LastPlayed { get; set; }
        [DataMember] public override int Time { get; set; }
        [DataMember] public override ulong MessageId { get; set; }
        [DataMember] public override ulong ChannelId { get; set; }
        [DataMember] public override ulong OwnerId { get { return UserId[0]; } set { UserId = new ulong[] { value }; } }

        [DataMember] private string FullMap //Converts map between char[,] and string
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


        private Pos FruitSecondPos => fruitSpawnPos + Dir.right; //Second tile which fruit will also occupy
        private int FruitTrigger1 => maxPellets - 70; //Amount of pellets remaining needed to spawn fruit
        private int FruitTrigger2 => maxPellets - 170;
        private int FruitScore => (pellets > FruitTrigger2) ? 1000 : 2000;
        private char FruitChar => (pellets > FruitTrigger2) ? 'x' : 'w';




        //Game data types

        public enum GameInput
        {
            None, Up, Left, Down, Right, Wait, Help, Fast,
        }

        public enum AiType
        {
            Blinky, Pinky, Inky, Clyde
        }

        public enum AiMode
        {
            Chase, Scatter, Frightened
        }



        // Game objects

        [DataContract]
        private class PacMan
        {
            [DataMember] public readonly Pos origin; //Position it started at
            [DataMember] public Pos pos; //Position on the map
            [DataMember] public Dir dir = Dir.none; //Direction it's facing
            [DataMember] public int power = 0; //Time left of power mode
            [DataMember] public int ghostStreak = 0; //Ghosts eaten during the current power mode

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
            [DataMember] public readonly Pos origin; //Tile it spawns in
            [DataMember] public readonly Pos corner; //Preferred corner
            [DataMember] public Pos pos; //Position on the map
            [DataMember] public Dir dir; //Direction it's facing
            [DataMember] public AiType type; //Ghost behavior type
            [DataMember] public AiMode mode; //Ghost behavior mode
            [DataMember] public int pauseTime; //Time remaining until it can move
            [DataMember] public bool exitRight = false; //It will exit the ghost box to the left unless modes have changed

            private Ghost() { }

            public Ghost(AiType type, Pos pos, Pos? corner) // Had to split pos and origin because of the deserializer
            {
                this.type = type;
                this.pos = pos;
                this.corner = corner ?? pos;
                origin = pos;
                pauseTime = GhostSpawnPauseTime[(int)type];
            }

            public void AI(PacManGame game)
            {
                //Decide mode
                if (game.pacMan.power <= 1) DecideMode(game);

                if (pauseTime > 0) // In the cage
                {
                    pos = origin;
                    dir = Dir.none;
                    pauseTime--;
                    if (JustChangedMode(game, fullCheck: true)) exitRight = true; // Exits to the right if modes changed
                    return;
                }

                if (mode == AiMode.Frightened && game.Time % 2 == 1) // If frightened, only moves on even turns
                {
                    if (JustChangedMode(game)) dir = dir.Opposite();
                    return;
                }

                //Decide target: tile it's trying to reach
                Pos target = default;

                switch (mode)
                {
                    case AiMode.Chase: //Normal
                        switch (type)
                        {
                            case AiType.Blinky:
                                target = game.pacMan.pos;
                                break;

                            case AiType.Pinky:
                                target = game.pacMan.pos + game.pacMan.dir.OfLength(4); //4 squares ahead
                                if (game.pacMan.dir == Dir.up) target += Dir.left.OfLength(4); //Intentional bug from the original arcade
                                break;

                            case AiType.Inky:
                                target = game.pacMan.pos + game.pacMan.dir.OfLength(2); //2 squares ahead
                                if (game.pacMan.dir == Dir.up) target += Dir.left.OfLength(2); //Intentional bug from the original arcade
                                target += target - game.ghosts[(int)AiType.Blinky].pos; //Opposite position relative to Blinky
                                break;

                            case AiType.Clyde:
                                if (Pos.Distance(pos, game.pacMan.pos) > 8) target = game.pacMan.pos;
                                else target = corner; //When close, gets scared
                                break;
                        }
                        break;

                    case AiMode.Scatter:
                        target = corner;
                        break;
                }


                //Decide movement

                if (game.map.At(pos) == CharDoor || game.map.At(pos + Dir.up) == CharDoor) // Exiting the cage
                {
                    dir = Dir.up;
                }
                else if (dir == Dir.up && game.map.At(pos + Dir.down) == CharDoor) // Getting away from the cage
                {
                    dir = exitRight ? Dir.right : Dir.left;
                }
                else if (JustChangedMode(game))
                {
                    dir = dir.Opposite();
                }
                else if (mode == AiMode.Frightened) // Turns randomly at intersections
                {
                    dir = Bot.Random.Choose(AllDirs.Where(p => p != dir.Opposite() && game.NonSolid(pos + p)).ToArray());
                }
                else //Track target
                {
                    exitRight = false;

                    float distance = float.PositiveInfinity;
                    foreach (Dir testDir in AllDirs.Where(x => x != dir.Opposite())) //Decides the direction that will get it closest to its target
                    {
                        Pos testPos = pos + testDir;

                        if (testDir == Dir.up && (game.map.At(testPos) == CharSoftWall || game.map.At(testPos) == CharSoftWallPellet)) continue; //Can't go up these places

                        if (game.NonSolid(testPos) && Pos.Distance(testPos, target) < distance) //Check if it can move to the tile and if this direction is better than the previous
                        {
                            dir = testDir;
                            distance = Pos.Distance(testPos, target);
                        }
                        //Console.WriteLine($"Target: {target.x},{target.y} / Ghost: {pos.x},{pos.y} / Test Dir: {(pos + testDir).x},{(pos + testDir).y} / Test Dist: {Pos.Distance(pos + testDir, target)}"); //For debugging AI
                    }
                }

                pos += dir;
                game.map.Wrap(ref pos);
            }

            public void DecideMode(PacManGame game)
            {
                if (game.Time < 4 * ScatterCycle  //In set cycles, a set number of turns is spent in scatter mode, up to 4 times
                    && (game.Time < 2 * ScatterCycle && game.Time % ScatterCycle < ScatterTime1
                    || game.Time >= 2 * ScatterCycle && game.Time % ScatterCycle < ScatterTime2)
                ) { mode = AiMode.Scatter; }
                else { mode = AiMode.Chase; }
            }

            private bool JustChangedMode(PacManGame game, bool fullCheck = false) // fullCheck detects changes to other ghosts
            {
                if (game.Time == 0) return false;
                if (mode == AiMode.Frightened && !fullCheck) return game.pacMan.power == PowerTime; // Detects the switch to Frightened, but not from it

                for (int i = 0; i < 2; i++) if (game.Time == i * ScatterCycle || game.Time == i * ScatterCycle + ScatterTime1) return true;
                for (int i = 2; i < 4; i++) if (game.Time == i * ScatterCycle || game.Time == i * ScatterCycle + ScatterTime2) return true;

                return fullCheck ? game.pacMan.power == PowerTime : false;
            }
        }




        // Game methods


        private PacManGame() : base() { } // Used by JSON deserializing

        public PacManGame(ulong ChannelId, ulong ownerId, string newMap, bool mobileDisplay, DiscordShardedClient client, LoggingService logger, StorageService storage)
            : base(ChannelId, new ulong[] { ownerId }, client, logger, storage)
        {
            this.mobileDisplay = mobileDisplay;

            // Map
            if (newMap == null) newMap = storage.BotContent["map"];
            else custom = true;

            FullMap = newMap; //Converts string into char[,]

            maxPellets = newMap.Count(c => c == CharPellet || c == CharPowerPellet || c == CharSoftWallPellet);
            pellets = maxPellets;

            // Game objects
            Pos playerPos = FindChar(CharPlayer).GetValueOrDefault();
            pacMan = new PacMan(playerPos);
            map.SetAt(pacMan.pos, ' ');

            fruitSpawnPos = FindChar(CharFruit) ?? new Pos(-1, -1);
            if (fruitSpawnPos.x >= 0) map.SetAt(fruitSpawnPos, ' ');

            ghosts = new List<Ghost>();
            Pos[] ghostCorners = new Pos[] { //Matches original game
                new Pos(map.X() - 3, -3),
                new Pos(2, -3),
                new Pos(map.X() - 1, map.Y()),
                new Pos(0, map.Y())
            };
            for (int i = 0; i < 4; i++)
            {
                Pos? ghostPos = FindChar(CharGhost);
                if (!ghostPos.HasValue) break;
                ghosts.Add(new Ghost((AiType)i, ghostPos.Value, ghostCorners[i]));
                map.SetAt(ghostPos.Value, ' ');
            }

            storage.StoreGame(this);
            if (custom) File.AppendAllText(BotFile.CustomMapLog, newMap);
        }




        public bool IsInput(IEmote emote, ulong userId = 1)
        {
            return GameInputs.ContainsKey(emote);
        }


        public void DoTurn(IEmote emote, ulong userId = 1)
        {
            if (State != State.Active) return;
            LastPlayed = DateTime.Now;

            GameInput input = GameInputs[emote];

            if (lastInput == GameInput.Help) // Closes help
            {
                lastInput = GameInput.None;
                return;
            }

            lastInput = input;

            if (input == GameInput.Help) return; // Opens help
            if (input == GameInput.Fast) // Toggle fastforward
            {
                fastForward = !fastForward;
                return;
            }

            oldScore = score;
            bool continueInput = true;

            Dir newDir = Dir.none;
            switch (input)
            {
                case GameInput.Up: newDir = Dir.up; break;
                case GameInput.Right: newDir = Dir.right; break;
                case GameInput.Down: newDir = Dir.down; break;
                case GameInput.Left: newDir = Dir.left; break;
            }


            int consecutive = 0;

            do
            {
                Time++;
                consecutive++;

                //Player
                if (newDir != Dir.none)
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

                //Fruit
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

                //Pellet collision
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
                        foreach (Ghost ghost in ghosts) ghost.mode = AiMode.Frightened;
                        continueInput = false;
                    }

                    if (pellets == 0)
                    {
                        State = State.Win;
                    }
                }

                //Ghosts
                foreach (Ghost ghost in ghosts)
                {
                    bool didAI = false;
                    while (true) //Checks player collision before and after AI
                    {
                        if (pacMan.pos == ghost.pos) //Collision
                        {
                            if (ghost.mode == AiMode.Frightened)
                            {
                                ghost.pauseTime = 6;
                                ghost.DecideMode(this); //Removes frightened State
                                score += 200 * (int)Math.Pow(2, pacMan.ghostStreak); //Each ghost gives double the points of the last
                                pacMan.ghostStreak++;
                            }
                            else State = State.Lose;

                            continueInput = false;
                            didAI = true; //Skips AI after collision
                        }

                        if (didAI || State != State.Active) break; //Doesn't run AI twice, or if the user already won

                        ghost.AI(this); //Full ghost behavior
                        didAI = true;
                    }
                }

                if (pacMan.power > 0) pacMan.power--;
                if (pacMan.power == 0) pacMan.ghostStreak = 0;

            } while (fastForward && continueInput && consecutive <= 20);

            if (State == State.Active)
            {
                storage.StoreGame(this); // Backup
            }
        }


        public override string GetContent(bool showHelp = true)
        {
            if (State == State.Cancelled && Channel is IGuildChannel) //So as to not spam
            {
                return "";
            }

            if (lastInput == GameInput.Help)
            {
                return storage.BotContent["gamehelp"].Replace("{prefix}", storage.GetPrefixOrEmpty(Guild));
            }

            try
            {
                StringBuilder display = new StringBuilder(); //The final display in string form
                char[,] displayMap = (char[,])map.Clone(); //The display array to modify

                //Scan replacements
                for (int y = 0; y < map.Y(); y++)
                {
                    for (int x = 0; x < map.X(); x++)
                    {
                        if (displayMap[x, y] == CharSoftWall) displayMap[x, y] = ' ';
                        else if (displayMap[x, y] == CharSoftWallPellet) displayMap[x, y] = CharPellet;

                        if (mobileDisplay) //Mode with simplified characters
                        {
                            if (!NonSolid(x, y) && displayMap[x, y] != CharDoor) displayMap[x, y] = '#'; //Walls
                            else if (displayMap[x, y] == CharPellet) displayMap[x, y] = '.'; //Pellets
                            else if (displayMap[x, y] == CharPowerPellet) displayMap[x, y] = 'o'; //Power pellets
                        }
                    }
                }

                //Adds fruit, ghosts and player
                if (fruitTimer > 0)
                {
                    displayMap[fruitSpawnPos.x, fruitSpawnPos.y] = FruitChar;
                    displayMap[FruitSecondPos.x, FruitSecondPos.y] = FruitChar;
                }
                foreach (Ghost ghost in ghosts)
                {
                    displayMap[ghost.pos.x, ghost.pos.y] = (ghost.mode == AiMode.Frightened) ? CharGhostFrightened : GhostAppearance[(int)ghost.type];
                }
                displayMap[pacMan.pos.x, pacMan.pos.y] = (State == State.Lose) ? CharPlayerDead : CharPlayer;

                //Converts 2d array to string
                for (int y = 0; y < displayMap.Y(); y++)
                {
                    for (int x = 0; x < displayMap.X(); x++)
                    {
                        display.Append(displayMap[x, y]);
                    }
                    display.Append('\n');
                }

                //Add text to the side
                string[] info = //Info panel
                {
                    $"┌{"───< Mobile Mode >───┐".If(mobileDisplay)}",
                    $"│ {"#".If(!mobileDisplay)}Time: {Time}",
                    $"│ {"#".If(!mobileDisplay)}Score: {score}{$" +{score - oldScore}".If(score - oldScore != 0)}",
                    $"│ {$"{"#".If(!mobileDisplay)}Power: {pacMan.power}".If(pacMan.power > 0)}",
                    $"│ ",
                    $"│ {CharPlayer} - Pac-Man{$": {pacMan.dir}".If(pacMan.dir != Dir.none)}",
                    $"│ ",
                    $"│ ", " │ ", " │ ", " │ ", //7-10: ghosts
                    $"│ ",
                    $"│ {($"{FruitChar}{FruitChar} - Fruit: {fruitTimer}").If(fruitTimer > 0)}",
                    $"└"
                };

                for (int i = 0; i < ghosts.Count; i++) //Ghost info
                {
                    char appearance = (ghosts[i].mode == AiMode.Frightened) ? CharGhostFrightened : GhostAppearance[i];
                    info[i + 7] = $"│ {appearance} - {(AiType)i}{$": {ghosts[i].dir}".If(ghosts[i].dir != Dir.none)}";
                }

                if (mobileDisplay)
                {
                    display.Insert(0, string.Join('\n', info) + "\n\n");
                }
                else
                {
                    for (int i = 0; i < info.Length && i < map.Y(); i++) //Insert info
                    {
                        int insertIndex = (i + 1) * displayMap.X(); //Skips ahead a certain amount of lines
                        for (int j = i - 1; j >= 0; j--) insertIndex += info[j].Length + 2; //Takes into account the added line length of previous info
                        display.Insert(insertIndex, $" {info[i]}");
                    }
                }

                //Code tags
                switch (State)
                {
                    case State.Lose:
                        display.Insert(0, "```diff\n");
                        display.Replace("\n", "\n-", 0, display.Length - 1); //All red
                        break;

                    case State.Win:
                        display.Insert(0, "```diff\n");
                        display.Replace("\n", "\n+", 0, display.Length - 1); //All green
                        break;

                    case State.Cancelled:
                        display.Insert(0, "```diff\n");
                        display.Replace("\n", "\n*** ", 0, display.Length - 1); //All gray
                        break;

                    default:
                        display.Insert(0, mobileDisplay ? "```\n" : "```css\n");
                        display.Append($"#Fastforward: {(fastForward ? "Active" : "Disabled")}");
                        break;
                }
                display.Append("```");

                if (State != State.Active || custom) //Secondary info box
                {
                    display.Append("```diff");
                    switch (State)
                    {
                        case State.Win: display.Append("\n+You won!"); break;
                        case State.Lose: display.Append("\n-You lost!"); ; break;
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
                       $"If this problem persists, please contact the author of the bot using the {storage.GetPrefixOrEmpty(Guild)}feedback command.```";
            }
        }


        public override EmbedBuilder GetEmbed(bool showHelp = true)
        {
            if (State == State.Cancelled && Channel is IGuildChannel) return CancelledEmbed();
            return null;
        }


        private Pos? FindChar(char c, int index = 0) //Finds the specified character instance in the map
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
        private bool NonSolid(Pos pos) //Defines which tiles in the map entities can move through
        {
            char ch = map.At(pos, wrap: true);
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
