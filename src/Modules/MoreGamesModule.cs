using System;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using Discord.WebSocket;
using PacManBot.Games;
using PacManBot.Services;
using PacManBot.Constants;
using static PacManBot.Games.GameUtils;

namespace PacManBot.Modules
{
    [Name("👾More Games"), Remarks("2")]
    public class MoreGamesModule : ModuleBase<SocketCommandContext>
    {
        private readonly DiscordShardedClient shardedClient;
        private readonly LoggingService logger;
        private readonly StorageService storage;


        public MoreGamesModule(DiscordShardedClient shardedClient, LoggingService logger, StorageService storage)
        {
            this.shardedClient = shardedClient;
            this.logger = logger;
            this.storage = storage;
        }



        [Command("pet"), Alias("clockagotchi"), Parameters("[command]"), Priority(0)]
        [Remarks("Adopt your own pet!")]
        [Summary("**__Pet Commands__**\n\n" +
            "**{prefix}pet** - Check on your pet or adopt if you don't have one\n" +
            "**{prefix}pet stats** - Check your pet's statistics and achievements\n" +
            "**{prefix}pet name <name>** - Name your pet\n" +
            "**{prefix}pet image <image>** - Give your pet an image\n\n" +
            "**{prefix}pet feed** - Fills your pet's Satiation and restores 1 Energy\n" +
            "**{prefix}pet play** - Fills your pet's Happinness and consumes 5 Energy\n" +
            "**{prefix}pet clean** - Fills your pet's Hygiene\n" +
            "**{prefix}pet sleep/wakeup** - Sleep to restore Energy over time\n\n" +
            "**{prefix}pet help** - This list of commands\n" +
            "**{prefix}pet pet** - Pet your pet\n" +
            "**{prefix}pet top** - Pet ranking\n" +
            "**{prefix}pet user <user>** - See another person's pet\n" +
            "**{prefix}pet release** - Gives your pet to a loving family that will take care of it (Deletes pet forever)")]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks | ChannelPermission.AddReactions)]
        public async Task Clockagotchi(string action = "", [Remainder]string args = null)
        {
            var pet = storage.GetUserGame<PetGame>(Context.User.Id);
            if (pet == null)
            {
                if (action == "")
                {
                    pet = new PetGame("", Context.User.Id, shardedClient, logger, storage);
                    storage.AddUserGame(pet);
                }
                else
                {
                    await ReplyAsync($"You don't have a pet yet! Simply do **{storage.GetPrefixOrEmpty(Context.Guild)}pet** to adopt one.", options: Utils.DefaultOptions);
                    return;
                }
            }

            switch (action)
            {
                case "":
                    await ReplyAsync(pet.GetContent(), false, pet.GetEmbed(Context.User as IGuildUser)?.Build(), Utils.DefaultOptions);
                    return;


                case "exact":
                    await ReplyAsync(pet.GetContent(), false, pet.GetEmbed(Context.User as IGuildUser, true)?.Build(), Utils.DefaultOptions);
                    return;


                case "stats":
                case "achievements":
                case "unlocks":
                    await ReplyAsync("", false, pet.GetEmbedAchievements(Context.User as IGuildUser)?.Build(), Utils.DefaultOptions);
                    return;


                case "feed":
                case "food":
                case "eat":
                    if (pet.Feed()) await Context.Message.AddReactionAsync(Bot.Random.Choose(PetGame.FoodEmotes).ToEmoji(), Utils.DefaultOptions);
                    else await ReplyAsync($"{CustomEmoji.Cross} Your pet is already full!");
                    return;


                case "play":
                case "fun":
                    if (pet.Play()) await Context.Message.AddReactionAsync(Bot.Random.Choose(PetGame.PlayEmotes).ToEmoji(), Utils.DefaultOptions);
                    else
                    {
                        string message = pet.happiness.Ceiling() == PetGame.MaxStat ? "Your pet doesn't want to play anymore!" : "Your pet is too tired to play! It needs 5 energy or more.";
                        await ReplyAsync($"{CustomEmoji.Cross} {message}", options: Utils.DefaultOptions);
                    }
                    return;


                case "clean":
                case "wash":
                    if (pet.Clean()) await Context.Message.AddReactionAsync(Bot.Random.Choose(PetGame.CleanEmotes).ToEmoji(), Utils.DefaultOptions);
                    else await ReplyAsync($"{CustomEmoji.Cross} Your pet is already clean!", options: Utils.DefaultOptions);
                    return;


                case "sleep":
                case "rest":
                    pet.UpdateStats(false);
                    if (pet.energy.Ceiling() == PetGame.MaxStat && !pet.asleep)
                    {
                        await ReplyAsync($"{CustomEmoji.Cross} Your pet is not tired!", options: Utils.DefaultOptions);
                    }
                    else
                    {
                        await ReplyAsync(Bot.Random.Choose(PetGame.SleepEmotes) + (pet.asleep ? " Your pet is already sleeping." : "Your pet is now asleep."), options: Utils.DefaultOptions);
                        if (!pet.asleep) pet.ToggleSleep();
                    }
                    return;


                case "wake":
                case "awaken": //?
                case "wakeup":
                    pet.UpdateStats(false);
                    await ReplyAsync(pet.asleep ? "🌅 You wake up your pet." : "🌅 Your pet is already awake.", options: Utils.DefaultOptions);
                    if (pet.asleep) pet.ToggleSleep();
                    return;


                case "release":
                    if (args != "yes")
                    {
                        await ReplyAsync($"❗ Are you sure you want to delete {pet.PetName}? It will be gone forever, along with your stats and achievements, and you can't get it back. " +
                                         $"Do **{storage.GetPrefixOrEmpty(Context.Guild)}pet release yes** to confirm.", options: Utils.DefaultOptions);
                    }
                    else
                    {
                        storage.DeleteUserGame(pet);
                        await ReplyAsync($"Goodbye {(string.IsNullOrWhiteSpace(pet.PetName) ? pet.Name : pet.PetName)}!", options: Utils.DefaultOptions);
                    }
                    return;


                case "name":
                    if (string.IsNullOrWhiteSpace(args)) await ReplyAsync($"{CustomEmoji.Cross} Please specify a name!", options: Utils.DefaultOptions);
                    else if (args.Length > 32) await ReplyAsync($"{CustomEmoji.Cross} Pet name can't go above 32 characters!", options: Utils.DefaultOptions);
                    else
                    {
                        pet.PetName = args;
                        await Context.Message.AddReactionAsync(CustomEmoji.Check, Utils.DefaultOptions);
                    }
                    return;


                case "image":
                    string url = args != null ? args : Context.Message.Attachments.FirstOrDefault()?.Url;
                    if (url == null && pet.PetImageUrl == null)
                    {
                        await ReplyAsync($"{CustomEmoji.Cross} Please specify an image!", options: Utils.DefaultOptions);
                    }
                    else
                    {
                        try
                        {
                            pet.PetImageUrl = url;
                            if (url == null) await ReplyAsync($"{CustomEmoji.Check} Pet image reset!", options: Utils.DefaultOptions);
                            else await Context.Message.AddReactionAsync(CustomEmoji.Check, Utils.DefaultOptions);
                        }
                        catch (FormatException)
                        {
                            await ReplyAsync($"{CustomEmoji.Cross} Invalid image link!\nYou can try uploading the image yourself.", options: Utils.DefaultOptions);
                        }
                    }
                    return;


                case "help":
                    var summary = typeof(MoreGamesModule).GetMethod(nameof(Clockagotchi)).GetCustomAttributes(typeof(SummaryAttribute), false).FirstOrDefault() as SummaryAttribute;
                    await ReplyAsync(summary?.Text.Replace("{prefix}", storage.GetPrefixOrEmpty(Context.Guild)) ?? "Couldn't get help", options: Utils.DefaultOptions);
                    return;


                case "pet":
                    var now = DateTime.Now;
                    if ((now - pet.lastPet) <= TimeSpan.FromSeconds(1.5)) return;
                    pet.lastPet = now;

                    await ReplyAsync(pet.Pet(), options: Utils.DefaultOptions);
                    return;


                case "top":
                case "rank":
                case "ranking":
                    var pets = storage.UserGames.Select(x => x as PetGame).Where(x => x != null).OrderByDescending(x => x.TimesPet);

                    int pos = 1;
                    var ranking = new StringBuilder();
                    ranking.Append($"**Out of {pets.Count()} pets:**\n");
                    foreach (var p in pets.Take(10))
                    {
                        ranking.Append($"\n**{pos}.** {p.TimesPet} pettings - ");
                        ranking.Append($"`{shardedClient.GetUser(p.OwnerId)?.Username.Replace("`", "´") ?? "Unknown"}'s {p.PetName.Replace("`", "´")}` ");
                        ranking.Append(string.Join(' ', p.achievements.GetIcons(showHidden: true, highest: true)));
                        pos++;
                    }

                    await ReplyAsync(ranking.ToString().Truncate(1999), options: Utils.DefaultOptions);
                    return;


                default:
                    await ReplyAsync($"Unknown pet command! Do **{storage.GetPrefixOrEmpty(Context.Guild)}pet help** for help", options: Utils.DefaultOptions);
                    return;
            }
        }


        [Command("pet user"), Alias("clockagotchi user"), Priority(1), HideHelp]
        [Summary("Checks another person's pet. See **{prefix}pet help** for more info.")]
        [BetterRequireBotPermission(ChannelPermission.EmbedLinks | ChannelPermission.AddReactions)]
        public async Task ClockagotchiUser(SocketGuildUser user = null)
        {
            if (user == null)
            {
                await ReplyAsync("You must specify a user!");
                return;
            }

            var pet = storage.GetUserGame<PetGame>(user.Id);

            if (pet == null) await ReplyAsync("This person doesn't have a pet :(", options: Utils.DefaultOptions);
            else await ReplyAsync(pet.GetContent(), false, pet.GetEmbed(user)?.Build(), Utils.DefaultOptions);
        }




        [Command("uno"), Parameters("[players]"), Priority(-1)]
        [Remarks("Play Uno with up to 10 friends and bots")]
        [ExampleUsage("uno\nuno @Pac-Man#3944")]
        [Summary("__**Commands:**__\n"
               + "\n • **{prefix}uno** - Starts a new Uno game, for up to 10 players. You can specify bots as opponents. Players can join or leave at any time."
               + "\n • **{prefix}uno join** - Join a game or invite a user or bot."
               + "\n • **{prefix}uno leave** - Leave the game or kick a bot or inactive user."
               + "\n • **{prefix}bump** - Move the game to the bottom of the chat."
               + "\n • **{prefix}cancel** - End the game in the current channel."
               + "\nᅠ{division}\n__**Rules:**__\n"
               + "\n • Each player is given 7 cards."
               + "\n • The current turn's player must choose a card that matches either the color, number or type of the last card."
               + "\n • If the player doesn't have any matching card, they will draw another card. If they still can't play they will skip a turn."
               + "\n • The first player to lose all of their cards wins the game."
               + "\n • **Special cards:** *Skip* cards make the next player skip a turn. *Reverse* cards change the turn direction, or act like *Skip* cards with only two players."
               + " *Draw* cards force the next player to draw cards and skip a turn. *Wild* cards let you choose the color, and will match with any card."
               + "\nᅠ")]
        [RequireContext(ContextType.Guild)]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartUno(params SocketGuildUser[] players)
        {
            players = new SocketGuildUser[] { Context.User as SocketGuildUser }.Union(players.Where(x => x.IsBot)).ToArray();
            await RunMultiplayerGame<UnoGame>(players);
        }


        [Command("uno help"), Alias("uno h"), Priority(1), HideHelp]
        [Summary("Gives rules and commands for the Uno game.")]
        [RequireContext(ContextType.Guild)]
        public async Task UnoHelp()
        {
            var summary = typeof(MoreGamesModule).GetMethod(nameof(StartUno)).GetCustomAttributes(typeof(SummaryAttribute), false).FirstOrDefault() as SummaryAttribute;
            await ReplyAsync(summary?.Text.Replace("{prefix}", storage.GetPrefix(Context.Guild)).Replace("{division}", "") ?? "Couldn't get help", options: Utils.DefaultOptions);
        }


        [Command("uno join"), Alias("uno add", "uno invite"), Priority(1), HideHelp]
        [Summary("Joins an ongoing Uno game in this channel. Fails if the game is full or if there aren't enough cards to draw for you.\n"
               + "You can also invite a bot or another user to play.")]
        [RequireContext(ContextType.Guild)]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task JoinUno(SocketGuildUser user = null)
        {
            bool self = false;
            if (user == null)
            {
                self = true;
                user = Context.User as SocketGuildUser;
            }

            var game = storage.GetGame<UnoGame>(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync($"There's no Uno game in this channel! Use **{storage.GetPrefix(Context.Guild)}uno** to start.", options: Utils.DefaultOptions);
                return;
            }
            if (game.UserId.Contains(user.Id))
            {
                await ReplyAsync($"{(self ? "You're" : "They're")} already playing!", options: Utils.DefaultOptions);
                return;
            }
            if (!self && !user.IsBot)
            {
                await ReplyAsync($"{user.Mention} You're being invited to play {game.Name}. Do **{storage.GetPrefix(Context.Guild)}uno join** to join.", options: Utils.DefaultOptions);
                return;
            }

            string failReason = game.AddPlayer(user.Id);
            if (failReason != null) await ReplyAsync($"{user.Mention} {"You ".If(self)}can't join this game: {failReason}", options: Utils.DefaultOptions);
            else await MoveGame();

            if (Context.BotCan(ChannelPermission.ManageMessages)) await Context.Message.DeleteAsync(options: Utils.DefaultOptions);
            else await Context.Message.AddReactionAsync(failReason == null ? CustomEmoji.Check : CustomEmoji.Cross, options: Utils.DefaultOptions);
        }


        [Command("uno leave"), Alias("uno remove", "uno kick"), Priority(1), HideHelp]
        [Summary("Leaves the Uno game in this channel.\nYou can also remove a bot or inactive player.")]
        [RequireContext(ContextType.Guild)]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task LeaveUno(SocketGuildUser user = null)
        {
            bool self = false;
            if (user == null)
            {
                self = true;
                user = Context.User as SocketGuildUser;
            }

            var game = storage.GetGame<UnoGame>(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync($"There's no Uno game in this channel! Use **{storage.GetPrefix(Context.Guild)}uno** to start.", options: Utils.DefaultOptions);
                return;
            }
            if (!game.UserId.Contains(user.Id))
            {
                await ReplyAsync($"{(self ? "You're" : "They're")} not playing!", options: Utils.DefaultOptions);
                return;
            }
            if (game.UserId.Length <= 2)
            {
                await CancelGame();
                return;
            }
            if (!self && !user.IsBot && (game.UserId[(int)game.Turn] != user.Id || (DateTime.Now - game.LastPlayed) < TimeSpan.FromMinutes(1)))
            {
                await ReplyAsync("To remove another user they have to be inactive for at least 1 minute during their turn.");
            }

            game.RemovePlayer(user.Id);
            await MoveGame();
            await Context.Message.AddReactionAsync(CustomEmoji.Check, Utils.DefaultOptions);
        }




        [Command("tictactoe"), Alias("ttt", "tic"), Priority(-1)]
        [Remarks("Play Tic-Tac-Toe with another user or the bot")]
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, you'll play against the bot.\n\n"
               + "You play by sending the number of a free cell (1 to 9) in chat while it is your turn, and to win you must make a line of 3 symbols in any direction\n\n"
               + "Do **{prefix}cancel** to end the game or **{prefix}bump** to move it to the bottom of the chat. The game times out after 5 minutes of inactivity.\n\n"
               + "You can also make the bot challenge another user or bot with **{prefix}ttt vs <opponent>**")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartTicTacToe(SocketGuildUser opponent = null)
        {
            await RunMultiplayerGame<TTTGame>(opponent ?? (IUser)Context.Client.CurrentUser, Context.User);
        }

        [Command("tictactoe vs"), Alias("ttt vs", "tic vs"), Priority(1), HideHelp]
        [Summary("Make the bot challenge a user... or another bot")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartTicTacToeVs(SocketGuildUser opponent)
        {
            await RunMultiplayerGame<TTTGame>(opponent, Context.Client.CurrentUser);
        }


        [Command("5ttt"), Alias("ttt5", "5tictactoe", "5tic"), Priority(-1)]
        [Remarks("Play a harder 5-Tic-Tac-Toe with another user or the bot")]
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, you'll play against the bot.\n\n"
               + "You play by sending the column and row of the cell you want to play, for example, \"C4\". The player who makes the **most lines of 3 symbols** wins. "
               + "However, if a player makes a lines of **4**, they win instantly.\n\n"
               + "Do **{prefix}cancel** to end the game or **{prefix}bump** to move it to the bottom of the chat. The game times out after 5 minutes of inactivity.\n\n"
               + "You can also make the bot challenge another user or bot with **{prefix}5ttt vs <opponent>**")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartTicTacToe5(SocketGuildUser opponent = null)
        {
            await RunMultiplayerGame<TTT5Game>(opponent ?? (IUser)Context.Client.CurrentUser, Context.User);
        }

        [Command("5ttt vs"), Alias("ttt5 vs", "5tictactoe vs", "5tic vs"), Priority(1), HideHelp]
        [Summary("Make the bot challenge a user... or another bot")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task Start5TicTacToeVs(SocketGuildUser opponent)
        {
            await RunMultiplayerGame<TTT5Game>(opponent, Context.Client.CurrentUser);
        }


        [Command("connect4"), Alias("c4", "four"), Priority(-1)]
        [Remarks("Play Connect Four with another user or the bot")]
        [Summary("You can choose a guild member to invite as an opponent using a mention, username, nickname or user ID. Otherwise, you'll play against the bot.\n\n"
               + "You play by sending the number of a free cell (1 to 7) in chat while it is your turn, and to win you must make a line of 3 symbols in any direction\n\n"
               + "Do **{prefix}cancel** to end the game or **{prefix}bump** to move it to the bottom of the chat. The game times out after 5 minutes of inactivity.\n\n"
               + "You can also make the bot challenge another user or bot with **{prefix}c4 vs <opponent>**")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartConnectFour(SocketGuildUser opponent = null)
        {
            await RunMultiplayerGame<C4Game>(opponent ?? (IUser)Context.Client.CurrentUser, Context.User);
        }

        [Command("connect4 vs"), Alias("c4 vs", "four vs"), Priority(1), HideHelp]
        [Summary("Make the bot challenge a user... or another bot")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks)]
        public async Task StartConnectFoureVs(SocketGuildUser opponent)
        {
            await RunMultiplayerGame<C4Game>(opponent, Context.Client.CurrentUser);
        }




        private async Task RunMultiplayerGame<TGame>(params IUser[] players) where TGame : MultiplayerGame
        {
            foreach (var otherGame in storage.Games)
            {
                if (otherGame.ChannelId == Context.Channel.Id)
                {
                    await ReplyAsync(otherGame.UserId.Contains(Context.User.Id) ?
                        $"You're already playing a game in this channel!\nUse **{storage.GetPrefixOrEmpty(Context.Guild)}cancel** if you want to cancel it." :
                        $"There is already a different game in this channel!\nWait until it's finished or try doing **{storage.GetPrefixOrEmpty(Context.Guild)}cancel**");
                    return;
                }
            }

            var playerIds = players.Select(x => x.Id).ToArray();

            TGame game = MultiplayerGame.New<TGame>(Context.Channel.Id, playerIds, shardedClient, logger, storage);

            while (!game.AllBots && game.AITurn) game.DoTurnAI();

            storage.AddGame(game);

            IUserMessage gameMessage;
            try
            {
                gameMessage = await ReplyAsync(game.GetContent(), false, game.GetEmbed()?.Build(), Utils.DefaultOptions);
            }
            catch (HttpException e)
            {
                storage.DeleteGame(game);
                await logger.Log(LogSeverity.Error, e.Reason); // Let's hope I figure out why I get Bad Gateway
                throw e;
            }
            game.MessageId = gameMessage.Id;

            while (game.State == State.Active)
            {
                if (game.AllBots)
                {
                    try
                    {
                        game.DoTurnAI();
                        if (game.MessageId != gameMessage.Id) gameMessage = await game.GetMessage();
                        game.CancelRequests();
                        if (gameMessage != null) await gameMessage.ModifyAsync(game.UpdateMessage, game.RequestOptions);
                    }
                    catch (Exception e) when (e is OperationCanceledException || e is TimeoutException || e is HttpException) { }

                    await Task.Delay(Bot.Random.Next(1000, 2001));
                }
                else await Task.Delay(5000);

                if ((DateTime.Now - game.LastPlayed) > game.Expiry)
                {
                    game.State = State.Cancelled;
                    storage.DeleteGame(game);
                    try
                    {
                        if (gameMessage.Id != game.MessageId) gameMessage = await game.GetMessage();
                        if (gameMessage != null) await gameMessage.ModifyAsync(game.UpdateMessage, Utils.DefaultOptions);
                    }
                    catch (HttpException) { } // Something happened to the message, we can ignore it
                    return;
                }
            }

            if (storage.Games.Contains(game)) storage.DeleteGame(game); // When playing against the bot
        }




        [Command("bump"), Alias("b", "refresh", "r", "move")]
        [Remarks("Move any game to the bottom of the chat")]
        [Summary("Moves the current game's message in this channel to the bottom of the chat, deleting the old one."
               + "This is useful if the game got lost in a sea of other messages, or if the game stopped responding")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks | ChannelPermission.AddReactions)]
        private async Task MoveGame()
        {
            var game = storage.GetGame(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync("There is no active game in this channel!", options: Utils.DefaultOptions);
                return;
            }

            try
            {
                var gameMessage = await game.GetMessage();
                if (gameMessage != null) await gameMessage.DeleteAsync(Utils.DefaultOptions); // Old message
            }
            catch (HttpException) { } // Something happened to the message, can ignore it

            var message = await ReplyAsync(game.GetContent(), false, game.GetEmbed()?.Build(), Utils.DefaultOptions);
            game.MessageId = message.Id;

            if (game is PacManGame pacManGame) await PacManModule.AddControls(pacManGame, message);
        }


        [Command("cancel"), Alias("end")]
        [Remarks("Cancel any game you're playing. Always usable by moderators")]
        [Summary("Cancels the current game in this channel, but only if you started or if nobody has played in over a minute. Always usable by users with the Manage Messages permission.")]
        [BetterRequireBotPermission(ChannelPermission.ReadMessageHistory | ChannelPermission.UseExternalEmojis | ChannelPermission.EmbedLinks | ChannelPermission.AddReactions)]
        public async Task CancelGame()
        {
            var game = storage.GetGame(Context.Channel.Id);
            if (game == null)
            {
                await ReplyAsync("There is no active game in this channel!", options: Utils.DefaultOptions);
                return;
            }

            if (game.UserId.Contains(Context.User.Id) || Context.UserCan(ChannelPermission.ManageMessages) || DateTime.Now - game.LastPlayed > TimeSpan.FromSeconds(60)
                || game is MultiplayerGame tpGame && tpGame.AllBots)
            {
                game.State = State.Cancelled;
                storage.DeleteGame(game);

                try
                {
                    var gameMessage = await game.GetMessage();
                    if (gameMessage != null)
                    {
                        await gameMessage.ModifyAsync(game.UpdateMessage, Utils.DefaultOptions);
                        if (game is PacManGame && Context.BotCan(ChannelPermission.ManageMessages)) await gameMessage.RemoveAllReactionsAsync(Utils.DefaultOptions);
                    }
                }
                catch (HttpException) { } // Something happened to the message, we can ignore it

                if (game is PacManGame pacManGame && Context.Guild != null)
                {
                    await ReplyAsync($"Game ended. Score won't be registered.\n**Result:** {pacManGame.score} points in {pacManGame.Time} turns", options: Utils.DefaultOptions);
                }
                else await Context.Message.AddReactionAsync(CustomEmoji.Check, Utils.DefaultOptions);
            }
            else await ReplyAsync("You can't cancel this game because someone else is still playing! Try again in a minute.", options: Utils.DefaultOptions);
        }
    }
}
