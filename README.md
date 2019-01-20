# Pac-Man Bot for Discord

![Discord Bots](https://discordbots.org/api/widget/status/398127484983443468.svg) ![Discord Bots](https://discordbots.org/api/widget/servers/398127484983443468.svg?noavatar=true)  
![Discord Bots](https://discordbots.org/api/widget/lib/398127484983443468.svg?noavatar=true) ![Discord Bots](https://discordbots.org/api/widget/owner/398127484983443468.svg?noavatar=true)  
[![paypal](https://img.shields.io/badge/Donate-PayPal-green.svg)](http://paypal.me/samrux)  

Play the best chat games for Discord: Pac-Man, Uno, Pets and more! Works in a server with friends or in direct messages with the bot.  
The objective is to bring the most enterntainment to you and your group, with the least amount of hassle and spam possible.

Features the following games:  
* Pac-Man: Turn-based controls, arcade-like ghost behavior and a global leaderboard: The original PacManBot game.
* Wakagotchi: Enjoy caring for a pet in a Discord-based tamagotchi clone.  
* ReactionRPG: Battle monsters and become stronger, or challenge your friends to battle - Enjoy the first release of this simple chat RPG!  
* Uno: Play with up to 10 friends and bots in the classic Uno card game.  
* Tic-Tac-Toe, Connect Four: Challenge your friends or the bot itself.  
* Rubik's Cube: Attempt to solve a real cube in chat form. Seriously.

[**Bot invite link here**](http://bit.ly/pacman-bot)  
[**Support server here**](https://discord.gg/hGHnfda)  

&nbsp;

## API Documentation

The full project documentation can be found [here](https://rawgit.com/Samrux/Pac-Man-Bot/master/_site/api/index.html).

&nbsp;

## Running your own instance of Pac-Man Bot

If you feel like the public bot doesn't fit your needs, you can host your own Pac-Man bot. For hosting, you will need to either dedicate a machine to keep it running, or use a paid VPS service such as [DigitalOcean](https://m.do.co/c/7cbf69c956b7), which I use.  
First, install the [.NET Core runtime here](https://www.microsoft.com/net/download).  
Once that's installed, you will need the bot's compiled files. If you're on 64-bit Linux and want to run the latest version, you can clone the [linux-release](https://github.com/Samrux/Pac-Man-Bot/tree/linux-release) branch. If you're on a different system or would like to maintain and run your own fork, please refer to *"Compiling Pac-Man Bot"* below.  
Make sure that the files found in [bin/](https://github.com/Samrux/Pac-Man-Bot/tree/master/bin) (`config.json` and `contents.json`) are in the same folder as the rest of the bot's compiled files.  
&nbsp;  
Then, you'll need to set-up a Discord application for the bot to attach to. For this, you'll need to go to the [developers](https://discordapp.com/developers/applications/) page, create an application, give it a name, and add a bot to it in the Bot tab. Make sure to give the bot a name and avatar, too.  
Once your application's ready, copy its token and put it as the value for `discordToken` in your `config.json` file.  
Finally, you can run the application from the `Pacman.dll` file. In Linux, you'd first give it permission to run with `chmod 777 Pacman.dll`, then you just do `./Pacman.dll`  
&nbsp;  
Congratulations on your new bot! To invite it to a server you own, go to its application's OAuth tab. Select the `bot` scope type, and any predefined permissions you'd like. You can then copy and use the generated invitation link.  
To further configure your instance of the bot, you can see all the variables you can add into your `config.cs` in [BotConfig.cs](https://github.com/Samrux/Pac-Man-Bot/blob/master/src/BotConfig.cs).  
&nbsp;  
Importantly, note that the bot uses many custom emotes throughout its commands and games. To be able to use and display all these emotes, your instance of the bot needs to be in the [Pac-Man Bot server](https://discord.gg/hGHnfda), where the emotes are located; I'll gladly add it there if you ask. Otherwise, refer to the *"Using different emotes"* section below.  

## Compiling Pac-Man Bot

Should you want to maintain a custom fork of Pac-Man Bot, it's very simple.  
First, you'll need to add the NuGet source for the Discord.Net library, for the project to use. An IDE such as Visual Studio would help you with this. The source link is https://www.myget.org/F/discord-net/api/v3/index.json   
&nbsp;  
To compile the bot, you'll need to install the [.NET Core SDK here](https://www.microsoft.com/net/download). In Windows, Visual Studio should install it for you.  
With .NET installed, you run a command such as this one:  

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
