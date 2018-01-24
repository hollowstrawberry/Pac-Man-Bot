using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PacManBot.Modules.PacManModule
{
    public class Game
    {
        static public List<Game> gameInstances = new List<Game>();

        public const string LeftEmoji = "⬅", UpEmoji = "⬆", DownEmoji = "⬇", RightEmoji = "➡", WaitEmoji = "⏸", RefreshEmoji = "🔃"; //Controls
        private const char CharPlayer = 'O', CharFruit = '$', CharGhost = 'G', CharDoor = '-', CharPellet = '·', CharPowerPellet = '●'; //Read from map
        private const char CharPlayerDead = 'X', CharGhostFrightened = 'E'; //Displayed
        private const int PowerTime = 20, ScatterCycle = 100, ScatterTime1 = 30, ScatterTime2 = 20; //Mechanics constants

        private readonly static Dir[] AllDirs = { Dir.up, Dir.left, Dir.down, Dir.right }; //Order of preference when deciding direction
        private readonly static char[] GhostAppearance = { 'B', 'P', 'I', 'C' };
        private readonly static int[] GhostSpawnPauseTime = { 0, 3, 15, 35 };

        public ulong channelId; //Which channel this game is located in
        public ulong messageId = 1; //The focus message of the game, for controls to work. Even if not set, it must be a number above 0
        public bool mobileDisplay = false;
        public State state = State.Active;
        public int score = 0;
        public int timer = 0; //How many turns have passed
        private int pellets;
        private readonly int maxPellets;
        private char[,] map;
        private Random random;
        private Player player;
        private List<Ghost> ghosts;
        private Fruit fruit;
        private Pos FruitSpawnPos; //Where all fruit will spawn
        private Pos FruitSecondPos => FruitSpawnPos + Dir.right; //Second tile which fruit will also occupy
        private Fruit[] fruitTypes; //Stores the fruits that will be available in this game

        private int FruitTrigger1 => maxPellets - 70; //Amount of pellets remaining needed to spawn fruit
        private int FruitTrigger2 => maxPellets - 170;


        public enum State { Active, Lose, Win }

        public enum AiType { Blinky, Pinky, Inky, Clyde }

        public enum AiMode { Chase, Scatter, Frightened }

        public enum Dir { none, up, down, left, right }

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
                    case Dir.up:    return new Pos(pos.x, pos.y - 1);
                    case Dir.down:  return new Pos(pos.x, pos.y + 1);
                    case Dir.left:  return new Pos(pos.x - 1, pos.y);
                    case Dir.right: return new Pos(pos.x + 1, pos.y);
                    default: return pos;
                }
            }

            public static float Distance(Pos pos1, Pos pos2) => (float)Math.Sqrt(Math.Pow(pos2.x - pos1.x, 2) + Math.Pow(pos2.y - pos1.y, 2));
        }

        private class Player
        {
            public Pos pos; //Position on the map
            public Pos origin; //Position it started at
            public Dir dir = Dir.none; //Direction it's facing
            public int power = 0; //Time left of power mode
            public int ghostStreak = 0; //Ghosts eaten during the current power mode

            public Player(Pos pos)
            {
                if (pos != null) this.pos = pos;
                else this.pos = new Pos(0, 0);
                origin = this.pos;
            }
        }

        private class Fruit
        {
            public int time = 0;
            public char char1, char2;
            public int points;

            public Fruit(char char1, char char2, int points)
            {
                this.char1 = char1;
                this.char2 = char2;
                this.points = points;
            }
        }

        private class Ghost
        {
            public Pos pos; //Position on the map
            public Pos target; //Tile it's trying to reach
            public Pos origin; //Tile it spawns in
            public Pos corner; //Preferred corner
            public Dir dir = Dir.none; //Direction it's facing
            public AiType type; //Ghost behavior type
            public AiMode mode = AiMode.Chase; //Ghost behavior mode
            public int pauseTime = 0; //Time remaining until it can move

            public Ghost(Pos pos, AiType type, Pos corner)
            {
                this.pos = pos;
                this.type = type;
                origin = pos;
                this.corner = corner ?? origin;
                pauseTime = GhostSpawnPauseTime[(int)type];
            }

            public void AI(Game game)
            {
                //Decide mode
                if (game.player.power == PowerTime) mode = AiMode.Frightened;
                else if (game.player.power <= 1) //Right after the last turn of power or any turn after
                {
                    if (game.timer < 4 * ScatterCycle &&
                            (game.timer  < 2 * ScatterCycle && game.timer % ScatterCycle < ScatterTime1
                          || game.timer >= 2 * ScatterCycle && game.timer % ScatterCycle < ScatterTime2)
                    ) { mode = AiMode.Scatter; }
                    else { mode = AiMode.Chase; }
                }

                if (pauseTime > 0)
                {
                    pauseTime--;
                    return;
                }

                //Decide target
                switch (mode)
                {
                    case AiMode.Chase: //Normal
                        switch(type)
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
                        if (type == AiType.Blinky && game.timer < 10 && game.ghosts.Count > 1) target = game.ghosts[(int)AiType.Pinky].corner; //So Blinky and Pinky go together at the start
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
                if (game.map[pos.x, pos.y] == CharDoor || game.map[(pos + Dir.up).x, (pos + Dir.up).y] == CharDoor)
                {
                    newDir = Dir.up; //If it's inside the cage
                }
                else //Track target
                {
                    float distance = 100f;
                    foreach (Dir testDir in AllDirs) //Decides the direction that will get it closest to its target
                    {
                        Pos tile = pos + testDir;

                        if (testDir == dir.Opposite() //Can't turn 180 degrees
                            && mode != AiMode.Frightened //Unless it's frightened
                            && !(game.timer < 4 * ScatterCycle && //Or it has just switched modes
                                    (game.timer  < 2 * ScatterCycle && game.timer % ScatterCycle == ScatterTime1
                                  || game.timer >= 2 * ScatterCycle && game.timer % ScatterCycle == ScatterTime2)
                                )
                        ) { continue; }

                        if (game.NonSolid(tile) && Pos.Distance(tile, target) < distance) //Check if it can move to the tile
                        {
                            distance = Pos.Distance(tile, target);
                            newDir = testDir;
                        }
                        //Console.WriteLine($"Target: {target.x},{target.y} / Ghost: {pos.x},{pos.y} / Test Dir: {(pos + testDir).x},{(pos + testDir).y} / Test Dist: {Pos.Distance(pos + testDir, target)}"); //For debugging AI
                    }
                }

                dir = newDir;
                pos += newDir;
                game.WrapAround(ref pos);
            }
        }


        public Game(ulong channelId, string customMap = null)
        {
            this.channelId = channelId;
            random = new Random();
            
            string[] newMap;
            if (customMap != null) newMap = customMap.Trim('\n').Trim().Trim('`').Split('\n');
            else newMap = File.ReadAllLines(Program.File_GameMap);
            LoadMap(newMap);
            
            maxPellets = pellets;

            Pos playerPos = FindChar(CharPlayer); //Set player
            if (playerPos == null) playerPos = new Pos(0, 0);
            player = new Player(playerPos);
            map[playerPos.x, playerPos.y] = ' ';

            Pos fruitPos = FindChar(CharFruit); //Set fruit defaults
            map[fruitPos.x, fruitPos.y] = ' ';
            FruitSpawnPos = fruitPos;
            fruitTypes = new Fruit[]{ new Fruit('x', 'x', 1000), new Fruit('w', 'w', 2000) };

            ghosts = new List<Ghost>(); //Set ghosts
            Pos[] ghostCorners = new Pos[] { new Pos(2, -2), new Pos(map.GetLength(0) - 3, -2), new Pos(0, map.GetLength(1)), new Pos(map.GetLength(0) - 1, map.GetLength(1)) }; //Matches original game
            for (int i = 0; i < 4; i++)
            {
                Pos ghostPos = FindChar(CharGhost);
                if (ghostPos == null) break;
                Pos cornerPos = ghostCorners[i % 2 == 0 ? i + 1 : i - 1]; //Goes in order: Top-Right Top-Left Bottom-Right Bottom-Left
                ghosts.Add(new Ghost(ghostPos, (AiType)i, cornerPos));
                map[ghostPos.x, ghostPos.y] = ' ';
            }
        }

        public void DoTick(Dir direction)
        {
            timer++;

            //Player
            if (direction != Dir.none) player.dir = direction;
            if (NonSolid(player.pos + direction)) player.pos += direction;
            WrapAround(ref player.pos);

            //Fruit
            if (fruit != null && fruit.time > 0)
            {
                fruit.time--;
                if (FruitSpawnPos == player.pos || FruitSecondPos == player.pos)
                {
                    score += fruit.points;
                    fruit.time = 0;
                }
            }

            //Pellets
            char tile = map[player.pos.x, player.pos.y];
            if (tile == CharPellet || tile == CharPowerPellet)
            {
                pellets--;
                if (pellets == FruitTrigger1 || pellets == FruitTrigger2)
                {
                    fruit = fruitTypes[(pellets >= FruitTrigger1) ? 0 : 1];
                    fruit.time = random.Next(25, 30 + 1);
                }
                else if (pellets == 0)
                {
                    state = State.Win;
                    return;
                }

                score += (tile == CharPowerPellet) ? 50 : 10;
                map[player.pos.x, player.pos.y] = ' ';
                if (tile == CharPowerPellet) player.power += PowerTime;
            }

            //Ghosts
            foreach (Ghost ghost in ghosts)
            {
                bool didAI = false;
                while (true) //Checks player collision before and after AI
                {
                    if (player.pos == ghost.pos) //Collision
                    {
                        if (player.power > 0)
                        {
                            ghost.pos = ghost.origin;
                            ghost.pauseTime = 5;
                            ghost.mode = AiMode.Chase;
                            ghost.dir = Dir.none;
                            score += 200 * (int)Math.Pow(2, player.ghostStreak);
                            player.ghostStreak++;
                        }
                        else state = State.Lose;

                        didAI = true; //Skips AI
                    }

                    if (didAI) break;

                    ghost.AI(this); //Full ghost behavior
                    didAI = true;
                }
            }

            if (player.power > 0) player.power--;
            if (player.power == 0) player.ghostStreak = 0;
        }

        public string Display
        {
            get
            {
                StringBuilder display = new StringBuilder(); //The final display in string form
                char[,] displayMap = (char[,])map.Clone(); //The display array to modify
                
                //Mode with simplified characters so it works better on mobile
                if (mobileDisplay)
                {
                    for (int y = 0; y < map.GetLength(1); y++)
                    {
                        for (int x = 0; x < map.GetLength(0); x++)
                        {
                            if (!NonSolid(x, y)) displayMap[x, y] = '#';
                            else if (map[x, y] == CharPellet) displayMap[x, y] = '.';
                            else if (map[x, y] == CharPowerPellet) displayMap[x, y] = '+';
                        }
                    }
                }

                //Adds fruit, ghosts and player
                if (fruit != null && fruit.time > 0)
                {
                    displayMap[FruitSpawnPos.x, FruitSpawnPos.y] = fruit.char1;
                    displayMap[FruitSecondPos.x, FruitSecondPos.y] = fruit.char2;
                }
                foreach (Ghost ghost in ghosts)
                {
                    displayMap[ghost.pos.x, ghost.pos.y] = (ghost.mode == AiMode.Frightened) ? CharGhostFrightened : GhostAppearance[(int)ghost.type];
                }
                displayMap[player.pos.x, player.pos.y] = (state == State.Lose) ? CharPlayerDead : CharPlayer;

                //Converts 2d array to string
                for (int y = 0; y < displayMap.GetLength(1); y++)
                {
                    for (int x = 0; x < displayMap.GetLength(0); x++)
                    {
                        display.Append(displayMap[x, y]);
                    }
                    display.Append('\n');
                }

                //Add text to the side
                string[] info =
                {
                    $" ┌{"<# MOBILE DISPLAY #>".If(mobileDisplay)}",
                    $" │ #Time: {timer}",
                    $" │ #Score: {score}",
                    $" │ {$"#Power: {player.power}".If(player.power > 0)}",
                    $" │ ",
                    $" │ {CharPlayer}{" - Pac-Man".Unless(mobileDisplay)}{$": {player.dir}".Unless(player.dir == Dir.None)}",
                    $" │ ",
                    $" │ ", " │ ", " │ ", " │ ", //7-10: ghosts
                    $" │ ",
                    $" │ {$"{fruit.char1}{fruit.char2} - Fruit: {fruit.time}".Unless(fruit == null || fruit.time <= 0)}",
                    $" └"
                };

                for (int i = 0; i < 4; i++) //Ghost info
                {
                    if (i + 1 > ghosts.Count) continue;
                    char appearance = (ghosts[i].mode == AiMode.Frightened) ? CharGhostFrightened : GhostAppearance[i];
                    info[i + 7] = $" │ {appearance}{$" - {(AiType)i}".Unless(mobileDisplay)}{$": {ghosts[i].dir}".Unless(ghosts[i].dir == Dir.none)}";
                }

                for (int i = 0; i < info.Length; i++) //Insert info
                {
                    int startIndex = i + i * displayMap.GetLength(0);
                    for (int j = i; j >= 0; j--) startIndex += info[j].Length;
                    display.Replace("\n", $"{info[i]}\n", startIndex, displayMap.GetLength(0) - 1);
                }

                //Code tags
                switch (state)
                {
                    case State.Active:
                        display.Insert(0, mobileDisplay ? "```\n" "```css\n");
                        break;

                    case State.Lose:
                        display.Insert(0, "```diff\n");
                        display.Replace("\n", "\n-", 0, display.Length - 1); //All red
                        break;

                    case State.Win:
                        display.Insert(0, "```diff\n");
                        display.Replace("\n", "\n+", 0, display.Length - 1); //All green
                        break;
                }
                display.Append("```");
                if (state != State.Active) display.Append($"```diff\n{(state == State.Win ? "+You won" : "-You lost")}!```");

                return display.ToString();
            }
        }

        private Pos FindChar(char c, int index = 0)
        {
            for (int y = 0; y < map.GetLength(1); y++)
            {
                for (int x = 0; x < map.GetLength(0); x++)
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
        private bool NonSolid(Pos pos)
        {
            WrapAround(ref pos);
            return (map[pos.x, pos.y] == ' ' || map[pos.x, pos.y] == CharPellet || map[pos.x, pos.y] == CharPowerPellet);
        }

        private void WrapAround(ref Pos pos)
        {
            if      (pos.x < 0) pos.x = map.GetLength(0) + pos.x;
            else if (pos.x > map.GetLength(0) - 1) pos.x -= map.GetLength(0);
            else if (pos.y < 0) pos.y = map.GetLength(1) + pos.y;
            else if (pos.y > map.GetLength(1) - 1) pos.y -= map.GetLength(1);
        }

        private void LoadMap(string[] lines)
        {
            int width = lines[0].Length;
            int height = lines.Length;

            char[,] newMap = new char[width, height];
            try
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        newMap[x, y] = lines[y].ToCharArray()[x];
                        if (newMap[x, y] == CharPellet || newMap[x, y] == CharPowerPellet) pellets++;
                    }
                }
            }
            catch { throw new Exception($"Invalid map in file {file}"); }

            this.map = newMap;
        }
    }
}
