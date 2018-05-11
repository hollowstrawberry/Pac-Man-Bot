using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Discord;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Services;

namespace PacManBot.Modules.PacMan
{
    public class InvalidMapException : Exception
    {
        public InvalidMapException() { }
        public InvalidMapException(string message) : base(message) { }
        public InvalidMapException(string message, Exception inner) : base(message, inner) { }
    }


    [DataContract] // Serializable to store in JSON
    public class GameInstance
    {
        //Constants

        static public readonly Dictionary<IEmote, GameInput> GameInputs = new Dictionary<IEmote, GameInput>() //Reaction controls
        {
            { "⬅".ToEmoji(), GameInput.Left },
            { "⬆".ToEmoji(), GameInput.Up },
            { "⬇".ToEmoji(), GameInput.Down },
            { "➡".ToEmoji(), GameInput.Right },
            { CustomEmoji.Help, GameInput.Help },
            { "⏭".ToEmoji(), GameInput.Fast }
        };

        private const int PowerTime = 20, ScatterCycle = 100, ScatterTime1 = 30, ScatterTime2 = 20;

        private const char CharPlayer = 'O', CharFruit = '$', CharGhost = 'G', CharSoftWall = '_', CharSoftWallPellet = '~'; // Read from map
        private const char CharDoor = '-', CharPellet = '·', CharPowerPellet = '●', CharPlayerDead = 'X', CharGhostFrightened = 'E'; // Displayed

        private readonly static char[] GhostAppearance = { 'B', 'P', 'I', 'C' }; // Matches aiType enum
        private readonly static int[] GhostSpawnPauseTime = { 0, 3, 15, 35 };
        private readonly static Dir[] AllDirs = { Dir.up, Dir.left, Dir.down, Dir.right }; // Order of preference when deciding direction

        public const string Folder = "games/";
        public const string Extension = ".json";



        // Fields

        private DiscordShardedClient client;
        private StorageService storage;
        private LoggingService logger;
        private Random random = new Random();
        private CancellationTokenSource messageEditCTS = new CancellationTokenSource();

        [DataMember] public bool custom = false;
        [DataMember] public readonly ulong channelId; //Which channel this game is located in
        [DataMember] public ulong ownerId; //Which user started this game
        [DataMember] public ulong messageId = 1; //The focus message of the game, for controls to work. Even if not set, it must be a number above 0 or else a call to get the message object will throw an error
        [DataMember] public int score = 0;
        [DataMember] public int time = 0; //How many turns have passed
        [DataMember] public State state = State.Active;
        [DataMember] public DateTime lastPlayed;
        [DataMember] public bool mobileDisplay = false;

        [IgnoreDataMember] private char[,] map;
        [DataMember] private int maxPellets;
        [DataMember] private int pellets; //Pellets remaining
        [DataMember] private int oldScore = 0; //Score obtained last turn
        [DataMember] private Player player;
        [DataMember] private List<Ghost> ghosts;
        [DataMember] private int fruitTimer = 0;
        [DataMember] private Pos fruitSpawnPos; //Where all fruit will spawn
        [DataMember] private GameInput lastInput = GameInput.None;
        [DataMember] private bool fastForward = false;



        // Properties

        [DataMember] private string FullMap //Converts map between char[,] and string
        {
            set
            {
                if (!value.ContainsAny(' ', CharSoftWall, CharPellet, CharPowerPellet, CharSoftWallPellet))
                {
                    throw new InvalidMapException("Map is completely solid");
                }

                string[] lines = value.Split('\n');
                int width = lines[0].Length, height = lines.Length;
                map = new char[width,height];
                
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
                for (int y = 0; y < map.LengthY(); y++)
                {
                    if (y > 0) stringMap.Append('\n');
                    for (int x = 0; x < map.LengthX(); x++)
                    {
                        stringMap.Append(map[x, y]);
                    }
                }
                return stringMap.ToString();
            }
        }

        public RequestOptions MessageEditOptions => new RequestOptions() { Timeout = 10000, RetryMode = RetryMode.RetryRatelimit, CancelToken = messageEditCTS.Token };
        public SocketGuild Guild => (client.GetChannel(channelId) as SocketGuildChannel)?.Guild;
        public string GameFile => $"{Folder}{channelId}{Extension}";

        private Pos FruitSecondPos => fruitSpawnPos + Dir.right; //Second tile which fruit will also occupy
        private int FruitTrigger1 => maxPellets - 70; //Amount of pellets remaining needed to spawn fruit
        private int FruitTrigger2 => maxPellets - 170;
        private int FruitScore => (pellets > FruitTrigger2) ? 1000 : 2000;
        private char FruitChar => (pellets > FruitTrigger2) ? 'x' : 'w';




        //Game data types

        public enum State { Active, Cancelled, Lose, Win }

        public enum GameInput { None, Up, Left, Down, Right, Wait, Help, Fast }

        public enum Dir { none, up, left, down, right }

        public enum AiType { Blinky, Pinky, Inky, Clyde }

        public enum AiMode { Chase, Scatter, Frightened }



        // Game objects

        public class Pos //Coordinate in the map
        {
            public int x, y;
            public Pos(int x, int y)
            {
                this.x = x;
                this.y = y;
            }

            public static bool operator !=(Pos pos1, Pos pos2) => !(pos1 == pos2);
            public static bool operator ==(Pos pos1, Pos pos2)
            {
                if (pos1 is null || pos2 is null) return ReferenceEquals(pos1, pos2);
                return pos1.x == pos2.x && pos1.y == pos2.y;
            }

            public static Pos operator +(Pos pos1, Pos pos2) => new Pos(pos1.x + pos2.x, pos1.y + pos2.y);
            public static Pos operator -(Pos pos1, Pos pos2) => new Pos(pos1.x - pos2.x, pos1.y - pos2.y);

            public static Pos operator +(Pos pos, Dir dir) //Moves position in the given direction
            {
                switch (dir)
                {
                    case Dir.up: return new Pos(pos.x, pos.y - 1);
                    case Dir.down: return new Pos(pos.x, pos.y + 1);
                    case Dir.left: return new Pos(pos.x - 1, pos.y);
                    case Dir.right: return new Pos(pos.x + 1, pos.y);
                    default: return pos;
                }
            }

            public static float Distance(Pos pos1, Pos pos2) => (float)Math.Sqrt(Math.Pow(pos2.x - pos1.x, 2) + Math.Pow(pos2.y - pos1.y, 2));
        }


        private class Player
        {
            public readonly Pos origin; //Position it started at
            public Pos pos; //Position on the map
            public Dir dir = Dir.none; //Direction it's facing
            public int power = 0; //Time left of power mode
            public int ghostStreak = 0; //Ghosts eaten during the current power mode

            public Player(Pos pos, Pos origin)
            {
                this.pos = pos;
                this.origin = origin;
            }
        }


        private class Ghost
        {
            public readonly Pos origin; //Tile it spawns in
            public readonly Pos corner; //Preferred corner
            public Pos pos; //Position on the map
            public Dir dir = Dir.none; //Direction it's facing
            public AiType type; //Ghost behavior type
            public AiMode mode; //Ghost behavior mode
            public int pauseTime; //Time remaining until it can move
            public bool exitRight = false; //It will exit the ghost box to the left unless modes have changed

            public Ghost(AiType type, Pos pos, Pos origin, Pos corner) // Had to split pos and origin because of the deserializer
            {
                this.type = type;
                this.pos = pos;
                this.origin = origin;
                this.corner = corner ?? origin;
                pauseTime = GhostSpawnPauseTime[(int)type];
            }

            public void AI(GameInstance game)
            {
                //Decide mode
                if (game.player.power <= 1) DecideMode(game);

                if (pauseTime > 0) // In the cage
                {
                    pos = origin;
                    dir = Dir.none;
                    pauseTime--;
                    if (JustChangedMode(game, checkAll: true)) exitRight = true; // Exits to the right if modes changed
                    return;
                }

                //Decide target: tile it's trying to reach
                Pos target = new Pos(0,0);

                switch (mode)
                {
                    case AiMode.Chase: //Normal
                        switch (type)
                        {
                            case AiType.Blinky:
                                target = game.player.pos;
                                break;

                            case AiType.Pinky:
                                target = game.player.pos;
                                target += game.player.dir.OfLength(4); //4 squares ahead
                                if (game.player.dir == Dir.up) target += Dir.left.OfLength(4); //Intentional bug from the original arcade
                                break;

                            case AiType.Inky:
                                target = game.player.pos;
                                target += game.player.dir.OfLength(2); //2 squares ahead
                                if (game.player.dir == Dir.up) target += Dir.left.OfLength(2); //Intentional bug from the original arcade
                                target += target - game.ghosts[(int)AiType.Blinky].pos; //Opposite position relative to Blinky
                                break;

                            case AiType.Clyde:
                                if (Pos.Distance(pos, game.player.pos) > 8) target = game.player.pos;
                                else target = corner; //When close, gets scared
                                break;
                        }
                        break;

                    case AiMode.Scatter:
                        target = corner;
                        break;

                    case AiMode.Frightened:
                        for (int i = 0; i < 20; i++)
                        {
                            target = pos + (Dir)(game.random.Next(1, 5)); //Random adjacent empty space, 20 attempts
                            if (game.NonSolid(target)) break;
                        }
                        break;
                }


                //Decide movement

                Dir newDir = Dir.none;

                if (game.MapAt(pos) == CharDoor || game.MapAt(pos + Dir.up) == CharDoor) // Exiting the cage
                {
                    newDir = Dir.up;
                }
                else if (dir == Dir.up && game.MapAt(pos + Dir.down) == CharDoor) // Getting away from the cage
                {
                    newDir = exitRight ? Dir.right : Dir.left;
                }
                else if (JustChangedMode(game))
                {
                    newDir = dir.Opposite();
                }
                else //Track target
                {
                    exitRight = false;

                    float distance = 1000f;
                    foreach (Dir testDir in AllDirs) //Decides the direction that will get it closest to its target
                    {
                        Pos testPos = pos + testDir;

                        if (testDir == Dir.up && (game.MapAt(testPos) == CharSoftWall || game.MapAt(testPos) == CharSoftWallPellet)) continue; //Can't go up these places
                        if (testDir == dir.Opposite()) continue; //Can't turn 180 degrees unless the direction was changed previously

                        if (game.NonSolid(testPos) && Pos.Distance(testPos, target) < distance) //Check if it can move to the tile and if this direction is better than the previous
                        {
                            newDir = testDir;
                            distance = Pos.Distance(testPos, target);
                        }
                        //Console.WriteLine($"Target: {target.x},{target.y} / Ghost: {pos.x},{pos.y} / Test Dir: {(pos + testDir).x},{(pos + testDir).y} / Test Dist: {Pos.Distance(pos + testDir, target)}"); //For debugging AI
                    }
                }

                dir = newDir;
                if (mode != AiMode.Frightened || game.time % 2 == 0) pos += dir; // If frightened, only moves on even turns
                game.WrapAround(ref pos);
            }

            public void DecideMode(GameInstance game)
            {
                if (game.time < 4 * ScatterCycle  //In set cycles, a set number of turns is spent in scatter mode, up to 4 times
                    && (game.time < 2 * ScatterCycle && game.time % ScatterCycle < ScatterTime1
                    || game.time >= 2 * ScatterCycle && game.time % ScatterCycle < ScatterTime2)
                ) { mode = AiMode.Scatter; }
                else { mode = AiMode.Chase; }
            }

            private bool JustChangedMode(GameInstance game, bool checkAll = false)
            {
                if (game.time == 0) return false;
                if (mode == AiMode.Frightened && !checkAll) return game.player.power == PowerTime; // If frightened it only counts as changing modes at the start (but during pausetime it scans anyway)

                for (int i = 0; i < 2; i++) if (game.time == i * ScatterCycle || game.time == i * ScatterCycle + ScatterTime1) return true;
                for (int i = 2; i < 4; i++) if (game.time == i * ScatterCycle || game.time == i * ScatterCycle + ScatterTime2) return true;

                return checkAll ? game.player.power == PowerTime : false;
            }
        }




        // Game methods

        private GameInstance() { } // Used by JSON deserializing

        public GameInstance(ulong channelId, ulong ownerId, string newMap, DiscordShardedClient client, StorageService storage, LoggingService logger)
        {
            this.client = client;
            this.storage = storage;
            this.logger = logger;
            this.channelId = channelId;
            this.ownerId = ownerId;
            lastPlayed = DateTime.Now;

            // Map
            if (newMap == null) newMap = storage.BotContent["map"];
            else custom = true;

            FullMap = newMap; //Converts string into char[,]

            maxPellets = newMap.Count(c => c == CharPellet || c == CharPowerPellet || c == CharSoftWallPellet);
            pellets = maxPellets;

            // Game objects
            Pos playerPos = FindChar(CharPlayer) ?? new Pos(0, 0);
            player = new Player(playerPos, playerPos);
            SetMapAt(player.pos, ' ');

            fruitSpawnPos = FindChar(CharFruit) ?? new Pos(-1, -1);
            if (fruitSpawnPos.x >= 0) SetMapAt(fruitSpawnPos, ' ');

            ghosts = new List<Ghost>();
            Pos[] ghostCorners = new Pos[] { //Matches original game
                new Pos(map.LengthX() - 3, -3),
                new Pos(2, -3),
                new Pos(map.LengthX() - 1, map.LengthY()),
                new Pos(0, map.LengthY())
            };
            for (int i = 0; i < 4; i++)
            {
                Pos ghostPos = FindChar(CharGhost);
                if (ghostPos == null) break;
                ghosts.Add(new Ghost((AiType)i, ghostPos, ghostPos, ghostCorners[i]));
                map[ghostPos.x, ghostPos.y] = ' ';
            }

            storage.StoreGame(this);
            if (custom) File.AppendAllText(BotFile.CustomMapLog, newMap);
        }


        public void DoTurn(GameInput input)
        {
            if (state != State.Active) return; //Failsafe

            lastPlayed = DateTime.Now;

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

            do
            {
                time++;

                //Player
                Dir newDir = (int)input < 5 ? (Dir)input : Dir.none;
                if (newDir != Dir.none)
                {
                    player.dir = newDir;
                    if (NonSolid(player.pos + newDir)) player.pos += newDir;
                    WrapAround(ref player.pos);
                }

                foreach (Dir dir in AllDirs) // Check perpendicular directions
                {
                    int diff = Math.Abs((int)player.dir - (int)dir);
                    if ((diff == 1 || diff == 3) && NonSolid(player.pos + dir)) continueInput = false; // Stops at intersections
                }

                //Fruit
                if (fruitTimer > 0)
                {
                    if (fruitSpawnPos == player.pos || FruitSecondPos == player.pos)
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
                char tile = MapAt(player.pos);
                if (tile == CharPellet || tile == CharPowerPellet || tile == CharSoftWallPellet)
                {
                    pellets--;
                    if ((pellets == FruitTrigger1 || pellets == FruitTrigger2) && fruitSpawnPos.x >= 0)
                    {
                        fruitTimer = random.Next(25, 30 + 1);
                    }

                    score += (tile == CharPowerPellet) ? 50 : 10;
                    map[player.pos.x, player.pos.y] = (tile == CharSoftWallPellet) ? CharSoftWall : ' ';
                    if (tile == CharPowerPellet)
                    {
                        player.power += PowerTime;
                        foreach (Ghost ghost in ghosts) ghost.mode = AiMode.Frightened;
                        continueInput = false;
                    }

                    if (pellets == 0)
                    {
                        state = State.Win;
                    }
                }

                //Ghosts
                foreach (Ghost ghost in ghosts)
                {
                    bool didAI = false;
                    while (true) //Checks player collision before and after AI
                    {
                        if (player.pos == ghost.pos) //Collision
                        {
                            if (ghost.mode == AiMode.Frightened)
                            {
                                ghost.pauseTime = 6;
                                ghost.DecideMode(this); //Removes frightened state
                                score += 200 * (int)Math.Pow(2, player.ghostStreak); //Each ghost gives double the points of the last
                                player.ghostStreak++;
                            }
                            else state = State.Lose;

                            continueInput = false;
                            didAI = true; //Skips AI after collision
                        }

                        if (didAI || state != State.Active) break; //Doesn't run AI twice, or if the user already won

                        ghost.AI(this); //Full ghost behavior
                        didAI = true;
                    }
                }

                if (player.power > 0) player.power--;
                if (player.power == 0) player.ghostStreak = 0;

            } while (fastForward && continueInput);

            if (state == State.Active)
            {
                storage.StoreGame(this); // Backup
            }
        }


        public string GetDisplay(bool showHelp = true)
        {
            if (lastInput == GameInput.Help)
            {
                return storage.BotContent["gamehelp"].Replace("{prefix}", storage.GetPrefixOrEmpty(Guild));
            }

            try
            {
                StringBuilder display = new StringBuilder(); //The final display in string form
                char[,] displayMap = (char[,])map.Clone(); //The display array to modify

                //Scan replacements
                for (int y = 0; y < map.LengthY(); y++)
                {
                    for (int x = 0; x < map.LengthX(); x++)
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
                displayMap[player.pos.x, player.pos.y] = (state == State.Lose) ? CharPlayerDead : CharPlayer;

                //Converts 2d array to string
                for (int y = 0; y < displayMap.LengthY(); y++)
                {
                    for (int x = 0; x < displayMap.LengthX(); x++)
                    {
                        display.Append(displayMap[x, y]);
                    }
                    display.Append('\n');
                }

                //Add text to the side
                string[] info = //Info panel
                {
                    $"┌{"───< Mobile Mode >───┐".If(mobileDisplay)}",
                    $"│ {"#".If(!mobileDisplay)}Time: {time}",
                    $"│ {"#".If(!mobileDisplay)}Score: {score}{$" +{score - oldScore}".If(score - oldScore != 0)}",
                    $"│ {$"{"#".If(!mobileDisplay)}Power: {player.power}".If(player.power > 0)}",
                    $"│ ",
                    $"│ {CharPlayer} - Pac-Man{$": {player.dir}".If(player.dir != Dir.none)}",
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
                    for (int i = 0; i < info.Length && i < map.LengthY(); i++) //Insert info
                    {
                        int insertIndex = (i + 1) * displayMap.LengthX(); //Skips ahead a certain amount of lines
                        for (int j = i - 1; j >= 0; j--) insertIndex += info[j].Length + 2; //Takes into account the added line length of previous info
                        display.Insert(insertIndex, $" {info[i]}");
                    }
                }

                //Code tags
                switch (state)
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
                        if (fastForward) display.Append("#Fastforward: Active");
                        break;
                }
                display.Append("```");

                if (state != State.Active || custom) //Secondary info box
                {
                    display.Append("```diff");
                    switch (state)
                    {
                        case State.Win: display.Append("\n+You won!"); break;
                        case State.Lose: display.Append("\n-You lost!"); ; break;
                        case State.Cancelled: display.Append("\n-Game has been ended!"); break;
                    }
                    if (custom) display.Append("\n*** Custom game: Score won't be registered. ***");
                    display.Append("```");
                }

                if (showHelp && state == State.Active && time < 5)
                {
                    display.Append($"\n(Confused? React with {CustomEmoji.Help} for help)");
                }

                return display.ToString();
            }
            catch (Exception e)
            {
                logger.Log(LogSeverity.Error, LogSource.Game, $"{e}");
                return $"```There was an error displaying the game. {"Make sure your custom map is valid. ".If(custom)}" +
                       $"If this problem persists, please contact the author of the bot using the {storage.GetPrefixOrEmpty(Guild)}feedback command.```";
            }
        }


        private Pos FindChar(char c, int index = 0) //Finds the specified character instance in the map
        {
            for (int y = 0; y < map.LengthY(); y++)
            {
                for (int x = 0; x < map.LengthX(); x++)
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
            WrapAround(ref pos);
            return (MapAt(pos) == ' ' || MapAt(pos) == CharPellet || MapAt(pos) == CharPowerPellet || MapAt(pos) == CharSoftWall || MapAt(pos) == CharSoftWallPellet);
        }


        private char MapAt(Pos pos) //Returns the character at the specified pos
        {
            WrapAround(ref pos);
            return map[pos.x, pos.y];
        }


        private void SetMapAt(Pos pos, char ch)
        {
            WrapAround(ref pos);
            map[pos.x, pos.y] = ch;
        }


        private void WrapAround(ref Pos pos) //Wraps the position from one side of the map to the other if it's out of bounds
        {
            if (pos.x < 0) pos.x = map.LengthX() + pos.x;
            else if (pos.x >= map.LengthX()) pos.x -= map.LengthX();
            if (pos.y < 0) pos.y = map.LengthY() + pos.y;
            else if (pos.y >= map.LengthY()) pos.y -= map.LengthY();
        }

        
        public void SetServices(DiscordShardedClient client, StorageService storage, LoggingService logger)
        {
            this.client = client;
            this.storage = storage;
            this.logger = logger;
        }


        public void CancelPreviousEdits()
        {
            messageEditCTS.Cancel();
            messageEditCTS = new CancellationTokenSource();
        }
    }
}
