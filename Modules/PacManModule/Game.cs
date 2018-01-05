using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PacManBot.Modules.PacManModule
{
    public class Game
    {
        static public List<Game> gameInstances = new List<Game>();

        public const string LeftEmoji = "⬅", UpEmoji = "⬆", DownEmoji = "⬇", RightEmoji = "➡", WaitEmoji = "⏹", RefreshEmoji = "🔃"; //Controls
        private const char PlayerChar = 'O', GhostChar = 'G', Pellet = '·', PowerPellet = '●'; //Read from map
        private const char PlayerDeadChar = 'X', GhostEatableChar = 'E'; //Displayed
        private static char[] GhostAppearance = { 'B', 'P', 'C', 'I' };


        public ulong channelId;
        public ulong messageId;
        public State state = State.Active;
        private char[,] board;
        private int score = 0;
        private int pellets = 0;
        private Player player;
        private List<Ghost> ghosts = new List<Ghost>();


        public enum State { Active, Lose, Win }

        public enum AI { Shadow, Speedy, Pokey, Bashful}

        public enum Dir { None, Up, Down, Left, Right }

        public class Pos
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
                if (ReferenceEquals(pos1, null) || ReferenceEquals(pos2, null)) return ReferenceEquals(pos1, pos2);
                return pos1.x == pos2.x && pos1.y == pos2.y;
            }

            public static int operator /(Pos pos1, Pos pos2) => Math.Abs(pos1.x - pos2.x) + Math.Abs(pos1.y - pos2.y); //Distance

            public static Pos operator +(Pos pos, Dir dir) //Moves position in the given direction
            {
                switch (dir)
                {
                    case Dir.Up:    return new Pos(pos.x, pos.y - 1);
                    case Dir.Down:  return new Pos(pos.x, pos.y + 1);
                    case Dir.Left:  return new Pos(pos.x - 1, pos.y);
                    case Dir.Right: return new Pos(pos.x + 1, pos.y);
                    default: return pos;
                }
            }
        }

        private class Player
        {
            public Pos pos;
            public Dir direction = Dir.None;
            public int powerMode = 0;

            public Player(Pos pos)
            {
                if (pos != null) this.pos = pos;
                else this.pos = new Pos(0, 0);
            }
        }

        private class Ghost
        {
            public Pos pos;
            public Pos target;
            public Pos origin;
            public AI type;

            public Ghost(Pos pos, AI type)
            {
                this.pos = pos;
                this.type = type;
                origin = pos;
            }

            public void DecideTarget(Player player)
            {
                AI aiType = type;
                if (aiType == AI.Bashful && new Random().Next(10) == 0) aiType = (AI)new Random().Next(3);

                switch (type)
                {
                    case AI.Shadow:
                        target = player.pos;
                        break;

                    case AI.Speedy:
                        target = player.pos + player.direction + player.direction; //Two squares in front
                        break;

                    case AI.Pokey:
                        Pos hidingSpot = new Pos(15, 14);
                        if (target == null || pos == hidingSpot) target = player.pos; //Chases the player after reaching its safe spot
                        else if (pos / player.pos < 7) target = hidingSpot; //Gets scared when it gets too close
                        break;

                    default:
                        target = player.pos;
                        break;
                }
            }
        }


        public Game(ulong channelId)
        {
            this.channelId = channelId;

            GrabBoardFromFile();

            Pos playerPos = FindChar(PlayerChar); //Set player
            if (playerPos == null) playerPos = new Pos(0, 0);
            player = new Player(playerPos);
            board[playerPos.x, playerPos.y] = ' ';

            for (int i = 0; i < 4; i++) //Set ghosts
            {
                Pos ghostPos = FindChar(GhostChar);
                if (ghostPos == null) continue;
                ghosts.Add(new Ghost(ghostPos, (AI)i));
                board[ghostPos.x, ghostPos.y] = ' ';
            }
        }

        public string Display { get
        {
            StringBuilder boardString = new StringBuilder(); //The final display
            char[,] displayBoard = (char[,])board.Clone(); //The temporary display array


            foreach (Ghost ghost in ghosts) displayBoard[ghost.pos.x, ghost.pos.y] = player.powerMode > 0 ? GhostEatableChar : GhostAppearance[(int)ghost.type]; //Adds ghosts
            displayBoard[player.pos.x, player.pos.y] = (state == State.Lose) ? PlayerDeadChar : PlayerChar; //Adds player

            for (int y = 0; y < displayBoard.GetLength(1); y++) //Converts 2d array to string
            {
                for (int x = 0; x < displayBoard.GetLength(0); x++)
                {
                    boardString.Append(displayBoard[x, y]);
                }
                boardString.Append('\n');
            }

            boardString.Replace("\n", $"  #Score: {score}\n", 0, displayBoard.GetLength(0) + 1);
            if (player.powerMode > 0) boardString.Replace("\n", $"  #Power: {player.powerMode}\n", boardString.ToString().Split('\n')[0].Length + 1, displayBoard.GetLength(0) + 1);

            switch (state)
            {
                case State.Active:
                    boardString.Insert(0, "```css\n");
                    break;

                case State.Lose:
                    boardString.Insert(0, "```diff\n");
                    boardString.Replace("\n", "\n-"); //All red
                    break;

                case State.Win:
                    boardString.Insert(0, "```diff\n");
                    boardString.Replace("\n", "\n+"); //All green
                    break;
            }
            boardString.Append("\n```");

            return boardString.ToString();
        }}

        public void DoTick(Dir direction)
        {
            //Player
            if (direction != Dir.None) player.direction = direction;
            if (NonSolid(player.pos + direction)) player.pos += direction;

            if      (player.pos.x < 0) player.pos.x = board.GetLength(0) - 1; //Wrapping around
            else if (player.pos.x > board.GetLength(0) - 1) player.pos.x = 0;
            else if (player.pos.y < 0) player.pos.y = board.GetLength(1) - 1;
            else if (player.pos.y > board.GetLength(1) - 1) player.pos.x = 0;

            //Ghosts
            foreach (Ghost ghost in ghosts)
            {
                if (player.pos == ghost.pos) //Player collision
                {
                    if (player.powerMode > 0)
                    {
                        ghost.pos = ghost.origin;
                        score += 200;
                    }
                    else state = State.Lose;
                    continue;
                }

                ghost.DecideTarget(player);
                ghost.pos += FindPath(ghost.pos, ghost.target); //Move if possible

                if (player.pos == ghost.pos) //Player collision again
                {
                    if (player.powerMode > 0)
                    {
                        ghost.pos = ghost.origin;
                        score += 200;
                    }
                    else state = State.Lose;
                }
            }

            //Pellets
            if (player.powerMode > 0) player.powerMode--;

            if (board[player.pos.x, player.pos.y] == Pellet)
            {
                pellets--;
                score += 10;
                board[player.pos.x, player.pos.y] = ' ';
            }
            else if (board[player.pos.x, player.pos.y] == PowerPellet)
            {
                pellets--;
                player.powerMode += 15;
                score += 50;
                board[player.pos.x, player.pos.y] = ' ';
            }

            if (pellets == 0) state = State.Win;
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

        private bool NonSolid(Pos pos, bool collideGhosts = false) => NonSolid(pos.x, pos.y, collideGhosts);
        private bool NonSolid(int x, int y, bool collideGhosts = false)
        {
            if (y < 0 && NonSolid(x, board.GetLength(1) - 1) //Wrapping around
                || y > board.GetLength(1) - 1 && NonSolid(x, 0)
                || x < 0 && NonSolid(board.GetLength(0) - 1, y)
                || x > board.GetLength(0) - 1 && NonSolid(0, y)
            ) return true;

            if (collideGhosts)
            {
                foreach (Ghost ghost in ghosts)
                {
                    if (ghost.pos == new Pos(x, y)) return false;
                }
            }

            return (board[x, y] == ' ' || board[x, y] == Pellet || board[x, y] == PowerPellet);
        }

        private Dir FindPath(Pos pos, Pos target)
        {
            if      (target.x < pos.x && NonSolid(pos + Dir.Left,  true)) return Dir.Left;
            else if (target.x > pos.x && NonSolid(pos + Dir.Right, true)) return Dir.Right;
            else if (target.y < pos.y && NonSolid(pos + Dir.Up,    true)) return Dir.Up;
            else if (target.y > pos.y && NonSolid(pos + Dir.Down,  true)) return Dir.Down;
            else return Dir.None;
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
                        if (board[x, y] == Pellet || board[x, y] == PowerPellet) pellets++;
                    }
                }
            }
            catch { throw new Exception("Invalid board"); }

            this.board = board;
        }
    }
}
