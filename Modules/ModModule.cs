using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PacManBot.Services;
using PacManBot.Constants;

namespace PacManBot.Modules
{
    [Name("<:staff:412019879772815361>Mod")]
    public class ModModule : ModuleBase<SocketCommandContext>
    {
        private readonly LoggingService logger;
        private readonly StorageService storage;

        public ModModule(LoggingService logger, StorageService storage)
        {
            this.logger = logger;
            this.storage = storage;
        }


        [Command("say"), Remarks("message — *Make the bot say anything*")]
        [Summary("Repeats back the message provided. Only users with the Manage Messages permission can use this command.")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task Say([Remainder]string text) => await ReplyAsync(text);

        [Command("clear"), Alias("c"), Remarks("[amount] — *Clear messages from this bot*")]
        [Summary("Clears all messages sent by *this bot only*, checking up to the amount of messages provided, or 10 messages by default. Only users with the Manage Messages permission can use this command.")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task ClearGameMessages(int amount = 10)
        {
            var messages = await Context.Channel.GetMessagesAsync(amount).FlattenAsync();
            foreach (IMessage message in messages)
            {
                if (message.Author.Id == Context.Client.CurrentUser.Id) await message.DeleteAsync(); //Remove all messages from this bot
            }
        }

        [Command("setprefix"), Remarks("prefix — *Set a custom prefix for this server (Admin)*")]
        [Summary("Change the custom prefix for this server. Only server Administrators can use this command.\nPrefixes can't contain \\*.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetServerPrefix(string newPrefix)
        {
            if (newPrefix.Contains("*"))
            {
                await ReplyAsync("Prefixes can't contain \\*.");
                return;
            }

            if (storage.prefixes.ContainsKey(Context.Guild.Id)) storage.prefixes[Context.Guild.Id] = newPrefix;
            else storage.prefixes.Add(Context.Guild.Id, newPrefix);

            try
            {
                string file = BotFile.Prefixes;
                string[] lines = File.ReadAllLines(file);

                int prefixIndex = lines.Length; //After everything else by default
                for (int i = 0; i < lines.Length; i++) if (lines[i].Split(' ')[0] == Context.Guild.Id.ToString()) prefixIndex = i; //Finds if the server already has a custom prefix

                string newLine = $"{Context.Guild.Id} {newPrefix}";
                if (prefixIndex >= lines.Length) //Outside the array
                {
                    File.AppendAllLines(file, new string[] { newLine });
                }
                else //Existing line
                {
                    lines[prefixIndex] = newLine;
                    File.WriteAllLines(file, lines);
                }

                await ReplyAsync($"{CustomEmojis.Check} Prefix for this server has been successfully set to '{newPrefix}'.");
                await logger.Log(LogSeverity.Verbose, $"Prefix for server {Context.Guild.Name} set to {newPrefix}");
            }
            catch (Exception e)
            {
                await logger.Log(LogSeverity.Error, $"{e}");
                string prefix = storage.GetPrefixOrEmpty(Context.Guild);
                await ReplyAsync($"{CustomEmojis.Cross} There was a problem storing the prefix on file. It might be reset the next time the bot restarts. Please try again or, if the problem persists, contact the bot author using **{prefix}feedback**.");
                throw new Exception("Couldn't modify prefix on file");
            }
        }

        [Command("togglewaka"), Remarks("— *Toggle \"waka\" autoresponse from the bot*")]
        [Summary("The bot normally responds every time a message contains purely multiples of \"waka\", unless it's turned off server-wide using this command. Requires the user to be a Moderator.")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task ToggleWakaResponse()
        {
            string wakafile = File.ReadAllText(BotFile.WakaExclude);
            bool nowaka = wakafile.Contains($"{Context.Guild.Id}");
            if (nowaka)
            {
                string newwakafile = wakafile.Replace($"{Context.Guild.Id} ", "");
                storage.wakaExclude = newwakafile;
                File.WriteAllText(BotFile.WakaExclude, newwakafile);
            }
            else
            {
                storage.wakaExclude += $"{Context.Guild.Id} ";
                File.AppendAllText(BotFile.WakaExclude, $"{Context.Guild.Id} ");
            }

            await ReplyAsync($"\"Waka\" responses turned **{(nowaka ? "on" : "off")}** in this server.");
        }
    }
}
