using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Extensions;
using PacManBot.Services;

namespace PacManBot.Commands
{
    /// <summary>
    /// The base for Pac-Man Bot modules, including their main services and some utilities.
    /// </summary>
    [PmRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.EmbedLinks |
                            ChannelPermission.UseExternalEmojis | ChannelPermission.AddReactions)]
    public abstract class BaseModule : ModuleBase<PmCommandContext>
    {
        /// <summary>Useful <see cref="RequestOptions"/> to be used in most Discord requests.</summary>
        public static readonly RequestOptions DefaultOptions = PmBot.DefaultOptions;

        /// <summary>Invisible character to be used in embeds.</summary>
        protected const string Empty = DiscordExtensions.Empty;


        /// <summary>Runtime settings of the bot.</summary>
        public PmConfig Config { get; set; }
        /// <summary>Content used throughout the bot.</summary>
        public PmContent Content => Config.Content;
        /// <summary>Logs everything in the console and on disk.</summary>
        public LoggingService Log { get; set; }
        /// <summary>Gives access to the bot's database.</summary>
        public StorageService Storage { get; set; }
        /// <summary>Gives access to input manipulation.</summary>
        public InputService Input { get; set; }



        /// <summary>Sends a message in the current context, using the default options if not specified.</summary>
        protected override async Task<IUserMessage> ReplyAsync(string message = null, bool isTTS = false, Embed embed = null, RequestOptions options = null)
            => await base.ReplyAsync(message, isTTS, embed, options ?? DefaultOptions);

        /// <summary>Sends a message in the current context, containing text and an embed, and using the default options if not specified.</summary>
        public Task<IUserMessage> ReplyAsync(object text, EmbedBuilder embed, RequestOptions options = null)
            => ReplyAsync(text?.ToString(), false, embed?.Build(), options);

        /// <summary>Sends a message in the current context containing only text, and using the default options if not specified.</summary>
        public Task<IUserMessage> ReplyAsync(object text, RequestOptions options = null)
            => ReplyAsync(text.ToString(), false, null, options);

        /// <summary>Sends a message in the current context containing only an embed, and using the default options if not specified.</summary>
        public Task<IUserMessage> ReplyAsync(EmbedBuilder embed, RequestOptions options = null)
            => ReplyAsync(null, false, embed.Build(), options);

        /// <summary>Sends a message in the current context containing only an embed, and using the default options if not specified.</summary>
        public Task<IUserMessage> ReplyAsync(Embed embed, RequestOptions options = null)
            => ReplyAsync(null, false, embed, options);


        /// <summary>Reacts to the command's calling message with a check or cross.</summary>
        public Task AutoReactAsync(bool success = true)
            => Context.Message.AutoReactAsync(success);


        /// <summary>Returns whether the next message by the user in this context is equivalent to "yes".</summary>
        public async Task<bool> GetYesResponse(int timeout = 30)
        {
            var response = (await GetResponse())?.Content.TrimStart(Context.Prefix).ToLowerInvariant();
            return response != null && (response == "y" || response == "yes");
        }


        /// <summary>Returns the first new message from the user in this context,
        /// or null if no message is received within the timeout in seconds.</summary>
        public async Task<SocketUserMessage> GetResponse(int timeout = 30)
        {
            return await Input.GetResponse(x =>
                x.Channel.Id == Context.Channel.Id && x.Author.Id == Context.User.Id,
                timeout);
        }

        /// <summary>Returns the first new message from the user in this context that satisfies additional conditions.
        /// The value will be null if no valid response is received within the timeout in seconds.</summary>
        public async Task<SocketUserMessage> GetResponse(Func<SocketUserMessage, bool> extraConditions, int timeout = 30)
        {
            return await Input.GetResponse(x =>
                x.Channel.Id == Context.Channel.Id && x.Author.Id == Context.User.Id && extraConditions(x),
                timeout);
        }
    }
}
