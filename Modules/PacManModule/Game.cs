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
        private const char CharPlayer = 'O', CharFruit = '$', CharGhost = 'G', CharCorner = '_', CharDoor = '-', CharPellet = '·', CharPowerPellet = '●'; //Read from map
        private const char CharPlayerDead = 'X', CharGhostFrightened = 'E'; //Displayed
        private const int PowerTime = 20, ScatterCycle = 100, ScatterTime1 = 30, ScatterTime2 = 20; //Mechanics constants
        private readonly static Dir[] allDirs = { Dir.up, Dir.left, Dir.down, Dir.right }; //Order of preference when deciding direction


        public ulong channelId; //Which channel this game is located in
        public ulong messageId = 1; //The message of the current game to manage controls. Even if not set, it must be a number above 0
        public State state = State.Active;
        public int score = 0;
        public int timer = 0; //How many turns have passed
        private int pellets;
        private readonly int maxPellets;
        private char[,] board;
        private Player player;
        private Fruit fruit;
        private List<Ghost> ghosts;
        private Random random;

        private int FruitTrigger1 => maxPellets - 70; //Amount of pellets remaining needed to spawn fruit
        private int FruitTrigger2 => maxPellets - 170;


        public enum State { Active, Lose, Win }

        public enum AiType { Blinky, Pinky, Inky, Clyde }

        public enum AiMode { Chase, Scatter, Frightened }

        public enum Dir { none, up, down, left, right }

        public class Pos //Coordinate in the board
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
            public Pos pos; //Position on the board
            public Pos origin; //Position it started at
            public Dir dir = Dir.none; //Direction it's facing
            public int power = 0; //Time left of power mode
            public int ghostsEaten = 0; //Ghosts eaten during the current power mode

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

            public static Pos spawnPos; //Where all fruit will spawn
            public static Pos secondPos => spawnPos + Dir.right; //Second tile which fruit will also occupy
            public static Fruit[] fruitType; //Stores the fruits that will be available

            public Fruit(char char1, char char2, int points)
            {
                this.char1 = char1;
                this.char2 = char2;
                this.points = points;
            }
        }

        private class Ghost
        {
            public Pos pos; //Position on the board
            public Pos target; //Tile it's trying to reach
            public Pos origin; //Tile it spawns in
            public Pos corner; //Preferred corner
            public Dir dir = Dir.none; //Direction it's facing
            public AiType type; //Ghost behavior type
            public AiMode mode = AiMode.Chase; //Ghost behavior mode
            public int pauseTime = 0; //Time remaining until it can move

            public readonly static char[] Appearance = { 'B', 'P', 'I', 'C' };
            public readonly static int[] SpawnPauseTime = { 0, 3, 15, 35 };

            public Ghost(Pos pos, AiType type, Pos corner)
            {
                this.pos = pos;
                this.type = type;
                origin = pos;
                this.corner = corner ?? origin;
                pauseTime = SpawnPauseTime[(int)type];
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
                        if (type == AiType.Blinky && game.timer < 10) target = game.ghosts[(int)AiType.Pinky].corner; //So Blinky and Pinky go together at the start
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
                if (game.board[pos.x, pos.y] == CharDoor || game.board[(pos + Dir.up).x, (pos + Dir.up).y] == CharDoor)
                {
                    newDir = Dir.up; //If it's inside the cage
                }
                else //Track target
                {
                    float distance = 100f;
                    foreach (Dir testDir in allDirs) //Decides the direction that will get it closest to its target
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


        public Game(ulong channelId)
        {
            this.channelId = channelId;
            random = new Random();

            GrabBoardFromFile();
            maxPellets = pellets;

            Pos playerPos = FindChar(CharPlayer); //Set player
            if (playerPos == null) playerPos = new Pos(0, 0);
            player = new Player(playerPos);
            board[playerPos.x, playerPos.y] = ' ';

            Pos fruitPos = FindChar(CharFruit); //Set fruit
            board[fruitPos.x, fruitPos.y] = ' ';
            Fruit.spawnPos = fruitPos;
            Fruit.fruitType = new Fruit[]{ new Fruit('x', 'x', 1000), new Fruit('w', 'w', 2000) };

            ghosts = new List<Ghost>();
            for (int i = 0; i < 4; i++) //Set ghosts
            {
                Pos ghostPos = FindChar(CharGhost);
                if (ghostPos == null) break;
                Pos cornerPos = FindChar(CharCorner, (i + 1) % 2); //Goes in order: Top-Right Top-Left Bottom-Right Bottom-Left
                ghosts.Add(new Ghost(ghostPos, (AiType)i, cornerPos));
                board[ghostPos.x, ghostPos.y] = ' ';
                board[cornerPos.x, cornerPos.y] = CharPellet;
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
                if (Fruit.spawnPos == player.pos || Fruit.secondPos == player.pos)
                {
                    score += fruit.points;
                    fruit.time = 0;
                }
            }

            //Pellets
            char tile = board[player.pos.x, player.pos.y];
            if (tile == CharPellet || tile == CharPowerPellet)
            {
                pellets--;
                if (pellets == FruitTrigger1 || pellets == FruitTrigger2)
                {
                    fruit = Fruit.fruitType[(pellets >= FruitTrigger1) ? 0 : 1];
                    fruit.time = random.Next(25, 30 + 1);
                }
                else if (pellets == 0)
                {
                    state = State.Win;
                    return;
                }

                score += (tile == CharPowerPellet) ? 50 : 10;
                board[player.pos.x, player.pos.y] = ' ';
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
                            score += 200 * (int)Math.Pow(2, player.ghostsEaten);
                            player.ghostsEaten++;
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
            if (player.power == 0) player.ghostsEaten = 0;
        }

        public string Display
        {
            get
            {
                StringBuilder boardString = new StringBuilder(); //The final display
                char[,] displayBoard = (char[,])board.Clone(); //The temporary display array

                //Adds fruit, ghosts and player
                if (fruit != null && fruit.time > 0)
                {
                    displayBoard[Fruit.spawnPos.x, Fruit.spawnPos.y] = fruit.char1;
                    displayBoard[Fruit.secondPos.x, Fruit.secondPos.y] = fruit.char2;
                }
                foreach (Ghost ghost in ghosts) displayBoard[ghost.pos.x, ghost.pos.y] = (ghost.mode == AiMode.Frightened) ? CharGhostFrightened : Ghost.Appearance[(int)ghost.type];
                displayBoard[player.pos.x, player.pos.y] = (state == State.Lose) ? CharPlayerDead : CharPlayer;

                //Converts 2d array to string
                for (int y = 0; y < displayBoard.GetLength(1); y++)
                {
                    for (int x = 0; x < displayBoard.GetLength(0); x++)
                    {
                        boardString.Append(displayBoard[x, y]);
                    }
                    boardString.Append('\n');
                }

                //Add text to the side
                string[] info = {
                    $" │ #Time: {timer}\n",
                    $" │ #Score: {score}\n",
                    $" │ #Power: {player.power}\n",
                    $" │\n",
                    $" │ {CharPlayer} - Pac-Man" + (player.dir == Dir.none ? "\n" : $": {player.dir}\n"),
                    $" │\n",
                    "", "", "", "", //6-9: ghosts, added right after
                    ((fruit == null || fruit.time <= 0) ? "\n" : $" │\n"), //Fruit
                    ((fruit == null || fruit.time <= 0) ? "\n" : $" │ {fruit.char1}{fruit.char2} - Fruit: {fruit.time}\n")
                };
                for (int i = 0; i < 4; i++) info[i + (info.Length - 6)] = $" │ {Ghost.Appearance[i]} - {(AiType)i}" + (ghosts[i].dir == Dir.none ? "\n" : $": {ghosts[i].dir}\n");

                for (int i = 0; i < info.Length; i++)
                {
                    int startIndex = 1 + i * displayBoard.GetLength(0);
                    for (int j = i; j >= 0; j--) startIndex += info[j].Length;
                    boardString.Replace("\n", info[i], startIndex, displayBoard.GetLength(0));
                }

                //Code tags
                switch (state)
                {
                    case State.Active:
                        boardString.Insert(0, "```css\n");
                        break;

                    case State.Lose:
                        boardString.Insert(0, "```diff\n");
                        boardString.Replace("\n", "\n-", 0, boardString.Length - 1); //All red
                        break;

                    case State.Win:
                        boardString.Insert(0, "```diff\n");
                        boardString.Replace("\n", "\n+", 0, boardString.Length - 1); //All green
                        break;
                }
                boardString.Append("```");


                return boardString.ToString();
            }
        }

        private Pos FindChar(char c, int index = 0)
        {
            for (int y = 0; y < board.GetLength(1); y++)
            {
                for (int x = 0; x < board.GetLength(0); x++)
                {
                    if (board[x, y] == c)
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

        private bool NonSolid(int x, int y, bool collideGhosts = false) => NonSolid(new Pos(x, y), collideGhosts);
        private bool NonSolid(Pos pos, bool collideGhosts = false)
        {
            WrapAround(ref pos);

            if (collideGhosts)
            {
                foreach (Ghost ghost in ghosts)
                {
                    if (ghost.pos == pos) return false;
                }
            }

            return (board[pos.x, pos.y] == ' ' || board[pos.x, pos.y] == CharPellet || board[pos.x, pos.y] == CharPowerPellet);
        }

        private void WrapAround(ref Pos pos)
        {
            if      (pos.x < 0) pos.x = board.GetLength(0) + pos.x;
            else if (pos.x > board.GetLength(0) - 1) pos.x -= board.GetLength(0);
            else if (pos.y < 0) pos.y = board.GetLength(1) + pos.y;
            else if (pos.y > board.GetLength(1) - 1) pos.y -= board.GetLength(1);
        }

        private void GrabBoardFromFile(string file = "board.txt")
        {
            string[] lines = File.ReadAllLines(file, Encoding.UTF8);
            int width = lines[0].Length;
            int height = lines.Length;

            char[,] board = new char[width, height];
            try
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        board[x, y] = lines[y].ToCharArray()[x];
                        if (board[x, y] == CharPellet || board[x, y] == CharPowerPellet || board[x, y] == CharCorner) pellets++;
                    }
                }
            }
            catch { throw new Exception("Invalid board"); }

            this.board = board;
        }
    }
}
