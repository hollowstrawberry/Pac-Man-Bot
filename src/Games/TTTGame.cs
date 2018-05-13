using System;
using System.Text;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;
using PacManBot.Constants;
using PacManBot.Services;
using static PacManBot.Games.GameUtils;

namespace PacManBot.Games
{
    class TTTGame : GameInstance
    {
        private static readonly Emote[] PlayerSymbol = new Emote[] { CustomEmoji.TTTx, CustomEmoji.TTTo, null, null };

        private static readonly TimeSpan _expiry = TimeSpan.FromMinutes(2);

        private static readonly Dictionary<string, GameInput> _gameInputs = new Dictionary<string, GameInput>()
        {
            {"1", GameInput.One},
            {"2", GameInput.Two},
            {"3", GameInput.Three},
            {"4", GameInput.Four},
            {"5", GameInput.Five},
            {"6", GameInput.Six},
            {"7", GameInput.Seven},
            {"8", GameInput.Eight},
            {"9", GameInput.Nine},
        };

        private Player[,] board;

        public override TimeSpan Expiry => _expiry;
        public override Dictionary<string, GameInput> GameInputs => _gameInputs;



        public TTTGame(ulong channelId, ulong[] userId, DiscordShardedClient client, LoggingService logger, StorageService storage)
            : base(channelId, userId, client, storage, logger)
        {
            board = new Player[3,3];
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    board[x, y] = Player.None;
                }
            }
        }


        public override void DoTurn(GameInput input)
        {
            base.DoTurn(input);

            int y = ((int)input - 1) / 3;
            int x = ((int)input - 1) % 3;

            if (board[x,y] != Player.None) return; // Cell is already occupied

            board[x,y] = turn;
            time++;

            if (IsWinner(turn)) winner = turn;
            else if (time == 9) winner = Player.Tie;
            else turn = turn == Player.Red ? Player.Blue : Player.Red;
        }


        public override EmbedBuilder GetEmbed(bool showHelp = true)
        {
            var description = new StringBuilder();

            int focus = winner == Player.None ? (int)turn : (int)winner;
            for (int i = 0; i < 2; i++)
            {
                description.Append($"{PlayerSymbol[i]} - {"`".If(i != focus)}{GetUser((Player)i).NameandNum().SanitizeMarkdown()}{"`".If(i != focus)}\n");
            }

            description.Append("ᅠ\n");

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    description.Append($"{PlayerSymbol[(int)board[x, y]]}" ?? (winner == Player.None ? $"{CustomEmoji.NumberCircle[1 + 3*y + x]}" : Player.None.Circle()));
                }
                description.Append('\n');
            }

            return new EmbedBuilder()
            {
                Title = winner == Player.None ? $"{turn} Player's turn" : winner == Player.Tie ? "It's a tie!" : $"{turn} is the winner!",
                Description = description.ToString(),
                Color = winner == Player.None ? turn.Color() : winner.Color(),
                ThumbnailUrl = winner == Player.None ? PlayerSymbol[(int)turn].Url : GetUser(winner)?.GetAvatarUrl(),
            };

        }


        public override string GetContent(bool showHelp = true)
        {
            return showHelp && winner == Player.None ? "Say a number (1-9) to place your symbol in that cell" : "";
        }


        public bool IsWinner(Player player)
        {
            uint count = 0;

            for (int y = 0; y < 3; y++) // Rows
            {
                for (int x = 0; x < 3; x++)
                {
                    if (board[x, y] == player) count++;
                    if (count == 3) return true;
                }
                count = 0;
            }

            for (int x = 0; x < 3; x++) // Columns
            {
                for (int y = 0; y < 3; y++)
                {
                    if (board[x, y] == player) count++;
                    if (count == 3) return true;
                }
                count = 0;
            }

            for (int d = 0; d < 3; d++) // Top-to-right diagonal
            {
                if (board[d, d] == player) count++;
                if (count == 3) return true;
            }
            count = 0;

            for (int d = 0; d < 3; d++) // Top-to-left diagonal
            {
                if (board[2-d, d] == player) count++;
                if (count == 3) return true;
            }

            return false;
        }


        private IUser GetUser(Player player)
        {
            return (int)player < userId.Length ? client.GetUser(userId[(int)player]) : null;
        }
    }
}
