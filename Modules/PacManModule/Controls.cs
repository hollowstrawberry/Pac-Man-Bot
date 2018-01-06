using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using static PacManBot.Modules.PacManModule.Game;
using System.IO;

namespace PacManBot.Modules.PacManModule
{
    static class Controls
    {
        public static async Task OnReactionAdded(IUserMessage message, SocketReaction reaction)
        {
            var user = reaction.User.Value;

            foreach (Game game in gameInstances)
            {
                if (message.Id == game.messageId) //Finds the game corresponding to this channel
                {
                    var direction = Dir.None;

                    if      (reaction.Emote.ToString() == UpEmoji   ) direction = Dir.Up;
                    else if (reaction.Emote.ToString() == RightEmoji) direction = Dir.Right;
                    else if (reaction.Emote.ToString() == DownEmoji ) direction = Dir.Down;
                    else if (reaction.Emote.ToString() == LeftEmoji ) direction = Dir.Left;

                    Console.WriteLine($"{DateTime.UtcNow.ToString("hh:mm:ss")} Game Input: {direction} by user {user.Username}#{user.Discriminator} in channel {message.Channel.Name}");

                    if (direction != Dir.None || reaction.Emote.ToString() == WaitEmoji) //Valid reaction input
                    {
                        game.DoTick(direction);

                        if (game.state == State.Active)
                        {
                            await message.ModifyAsync(m => m.Content = game.Display); //Update display
                            await message.RemoveReactionAsync(reaction.Emote, user);
                        }
                        else
                        {
                            gameInstances.Remove(game);
                            Console.WriteLine($"{DateTime.UtcNow.ToString("hh:mm:ss")} ({game.state}) Achieved score {game.score} in {game.timer} moves on channel {(message.Author as SocketGuildUser).Guild.Name}/{message.Channel} last controlled by user {user.Username}#{user.Discriminator}\n");
                            File.AppendAllText("scoreboard.txt", $"\n{game.state} {game.score} {game.timer} {user.Id} \"{user.Username}#{user.Discriminator}\" \"{DateTime.Now.ToString("o")}\" \"{(message.Author as SocketGuildUser).Guild.Name}/{message.Channel}\"");
                            await message.ModifyAsync(m => m.Content = game.Display + ((game.state == State.Win) ? "```diff\n+You won!```" : "```diff\n-You lost!```"));
                            await message.RemoveAllReactionsAsync();
                        }
                    }
                    else
                    {
                        await message.RemoveReactionAsync(reaction.Emote, user);
                    }
                }
            }
        }
    }
}
