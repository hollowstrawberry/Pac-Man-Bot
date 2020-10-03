# Pac-Man Bot for Discord

![Discord Bots](https://discordbots.org/api/widget/status/398127484983443468.svg) ![Discord Bots](https://discordbots.org/api/widget/servers/398127484983443468.svg?noavatar=true)  
![Discord Bots](https://discordbots.org/api/widget/lib/398127484983443468.svg?noavatar=true) ![Discord Bots](https://discordbots.org/api/widget/owner/398127484983443468.svg?noavatar=true)  
[![paypal](https://img.shields.io/badge/Donate-PayPal-green.svg)](http://paypal.me/samrux)  

Play the best chat games for Discord: Pac-Man, Uno, Hangman, Pets and more! Works in a server with friends or in direct messages with the bot.  
The objective is to bring the most enterntainment to you and your group, with the least amount of hassle and spam possible.

Features the following games:  
* Uno: Play with up to 10 friends and bots in the classic Uno card game.  
* Hangman: Everyone has to guess the random word, or a word chosen by you!
* Wakagotchi: Enjoy caring for a pet in a Discord-based tamagotchi clone.  
* ReactionRPG: Battle monsters and become stronger, or challenge your friends to battle - Enjoy this simple chat RPG!  
* Tic-Tac-Toe, Connect Four: Challenge your friends or the bot itself.  
* Code Break: Uncover the secret code in this public puzzle game.
* Minesweeper: A basic rendition of minesweeper using the new spoiler feature.
* Rubik's Cube: Attempt to solve a real cube in chat form. Seriously.
* Pac-Man: Turn-based controls, arcade-like ghost behavior and a global leaderboard: The original PacManBot game.

[**Bot invite link here**](http://bit.ly/pacman-bot)  
[**Support server here**](https://discord.gg/hGHnfda)  

&nbsp;

## Compiling Pac-Man Bot

Should you want to maintain a custom fork of Pac-Man Bot, here are the main steps.
 
To be able to compile the bot, you'll need to install the [.NET Core SDK here](https://www.microsoft.com/net/download). In Windows, Visual Studio should install it for you.  

Before compiling, you need to add the NuGet package of the DSharpPlus library, by first adding the nuget source: https://nuget.emzi0767.com/ . If you're using an IDE like Visual Studio or Rider, you can add it through there.
To compile, you use command such as this one:  

    dotnet publish PacManBot.csproj --runtime %RUNTIME% --configuration Release --self-contained

Where `%RUNTIME%` is the system you'll be building for, like `linux-x64` or `win-x64`. For a Raspberry Pi, use `linux-arm` (you can't build the bot on a Pi, but you can run it on one).  
The command will generate a `netcoreapp2.0/%RUNTIME%/publish/` folder in the target folder of the project's active configuration. The self-contained program will be inside.  


### Using different emotes

I recommend using my own copies of the emotes in the [Pac-Man Bot server](https://discord.gg/hGHnfda), which your bot can join if you ask.  
If you instead want to use your own copy of the emotes, here are the steps:

1. Grab the emote images from the [_Resources/Emotes/](https://github.com/Samrux/Pac-Man-Bot/tree/master/_Resources/Emotes) folder.  
2. Upload them to a Discord server that your bot has access to.  
3. Obtain all of their codes. You can do this quickly using the bot's 'emotes' developer command.
4. Modify your `src/Constants/CustomEmoji.cs` file with all the new codes.  
5. You can then build the bot again and test if the emotes are displaying correctly.

&nbsp;  
&nbsp;  

![Alt](https://raw.githubusercontent.com/Samrux/Pac-Man-Bot/master/_Resources/Avatar.png)
