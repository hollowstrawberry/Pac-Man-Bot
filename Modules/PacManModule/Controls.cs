using System;
using System.IO;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using static PacManBot.Modules.PacManModule.Game;


namespace PacManBot.Modules.PacManModule
{
    static class Controls
    {
        public static async Task ExecuteInput(SocketCommandContext context, SocketReaction reaction)
        {
            var user = reaction.User.Value;
            var message = context.Message;
            var guild = context.Guild;

            foreach (Game game in gameInstances)
            {
                if (message.Id == game.messageId && game.state == State.Active) //Finds the game corresponding to this channel
                {
                    var direction = Dir.none;
                    switch (reaction.Emote.ToString())
                    {
                        case UpEmoji: direction = Dir.up; break;
                        case RightEmoji: direction = Dir.right; break;
                        case DownEmoji: direction = Dir.down; break;
                        case LeftEmoji: direction = Dir.left; break;
                    }

                    string channelName = (message.Author as SocketGuildUser != null ? $"{(message.Author as SocketGuildUser).Guild.Name}/" : "") + message.Channel;

                    Console.WriteLine($"{DateTime.UtcNow.ToString("hh:mm:ss")} Game Input: {direction} by user {user.Username}#{user.Discriminator} in channel {channelName}");

                    if (direction != Dir.none || reaction.Emote.ToString() == WaitEmoji) //Valid reaction input
                    {
                        game.DoTick(direction);

                        if (game.state != State.Active)
                        {
                            gameInstances.Remove(game);
                            if (game.score > 0 && !game.custom)
                            {
                                Console.WriteLine($"{DateTime.UtcNow.ToString("hh:mm:ss")} ({game.state}) Achieved score {game.score} in {game.time} moves on channel {channelName} last controlled by user {user.Username}#{user.Discriminator}");
                                File.AppendAllText(Program.File_Scoreboard, $"\n{game.state} {game.score} {game.time} {user.Id} \"{user.Username}#{user.Discriminator}\" \"{DateTime.Now.ToString("o")}\" \"{channelName}\"");
                            }
                        }

                        await message.ModifyAsync(m => m.Content = game.Display); //Update display
                    }

                    if (guild != null && guild.CurrentUser.GuildPermissions.ManageMessages) //Can remove reactions
                    {
                        if (game.state == State.Active) await message.RemoveReactionAsync(reaction.Emote, user);
                        else await message.RemoveAllReactionsAsync();
                    }
                }
            }
        }
    }
}
