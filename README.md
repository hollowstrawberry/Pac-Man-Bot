# Pac-Man Bot for Discord

![Discord Bots](https://discordbots.org/api/widget/status/398127484983443468.svg) ![Discord Bots](https://discordbots.org/api/widget/servers/398127484983443468.svg?noavatar=true)  
![Discord Bots](https://discordbots.org/api/widget/lib/398127484983443468.svg?noavatar=true) ![Discord Bots](https://discordbots.org/api/widget/owner/398127484983443468.svg?noavatar=true)  

Play Pac-Man in a Discord chat! Works in a server with friends or in direct messages with the bot. Featuring turn-based gameplay with reaction controls, the focus is low spam and original arcade game fidelity. Get a spot on the global leaderboard!

If Pac-Man isn't your thing, there are several other minigames:
* Pets: Enjoy your own simple tamagotchi clone.
* Uno: Play with up to 10 friends and bots.
* Tic-Tac-Toe, Connect Four: Challenge your friends or the bot itself!

[**Bot invite link here**](http://bit.ly/pacman-bot)  
[**Support server here**](https://discord.gg/hGHnfda)  

&nbsp;

## Running your own instance of Pac-Man Bot

If you feel like the public bot doesn't fit your needs, you can host your own Pac-Man bot. For hosting, you will need to either dedicate a machine to keep it running, or use a paid VPS service such as [DigitalOcean](https://m.do.co/c/7cbf69c956b7), which I use.  

You will first need the bot's compiled files. If you're on 64-bit Linux and want to run the latest version, you can pull the `linux-release` branch of the repository. If you're on a different system or would like to maintain and run your own fork, please refer to *"Compiling Pac-Man Bot"* below.  
Make sure that the files found in `bin/` in the master branch (`config.json`, `contents.json`, `database.sqlite`) are in the same folder as the rest of the bot's files.  
&nbsp;  
Then, you'll need to set-up a Discord application for the bot to attach to. For this, you'll need to go to the [developers](https://discordapp.com/developers/applications/) page, create an application, give it a name, and add a bot to it in the Bot tab. Make sure to give the bot a name and avatar, too.  
Once your application's ready, copy its token and put it as the value for `discordToken` in the `config.json` file.  
Finally, you can run the application from the `Pacman.dll` file. In Linux, you'd first give it the right permissions with `chmod -x Pacman.dll`, then you just do `./Pacman.dll`  
&nbsp;  
Congratulations on your new bot! To invite it to a server you own, go to its application's OAuth tab. Select the `bot` scope type, and any predefined permissions you'd like. You can then copy and use the generated invitation link.  
&nbsp;  
Importantly, please note that the bot uses many custom emotes throughout its commands and games. To be able to use and display all these emotes, your instance of the bot needs to be in the [Pac-Man Bot server](https://discord.gg/hGHnfda), where the emotes are located; I'll gladly add it there if you ask. Otherwise, refer to the *"Using different emotes"* section below.

## Compiling Pac-Man Bot

Should you want to maintain a custom fork of Pac-Man Bot, it's very simple.  
First, you'll need to add the NuGet source for the Discord.Net library, for the project to use. An IDE such as Visual Studio would help you with this. The source link is https://www.myget.org/F/discord-net/api/v3/index.json   
&nbsp;  
To compile the bot, you'll need .NET. The install process varies with your operating system. In Windows, Visual Studio should install it for you. Otherwise, [here's a link](https://www.microsoft.com/net/learn/get-started-with-dotnet-tutorial).  
With .NET installed, you run a command such as this one:  

    dotnet publish PacManBot.csproj --runtime %RUNTIME% --configuration Release --self-contained

Where `%RUNTIME%` is the system you'll be building for, like `linux-x64` or `win-x64`. For a Raspberry Pi, use `linux-arm` (you can't build the bot on a Pi, but you can run it on one).  
The command will generate a `netcoreapp2.0/%RUNTIME%/publish/` folder in the target folder of the project's active configuration. The self-contained program will be inside.  


### Using different emotes

I recommend using my own copies of the emotes in the [Pac-Man Bot server](https://discord.gg/hGHnfda), which your bot can join if you ask.  
If you instead want to use your own copy of the emotes, here are the steps:

1. Grab the emote images from the `_Resources/Emotes` folder of the master branch.  
2. Upload them to a Discord server that your bot has access to.  
3. Obtain all of their codes. There are bots to do this automatically, but you can also send an emote's code in chat by putting a backslash before it in a message, like: `\:pacman:`  
4. Modify the `src/Constants/CustomEmoji.cs` file with all the new codes.  
5. You can then build the bot again and test if the emotes are now displaying correctly.

&nbsp;  
&nbsp;  

![Alt](https://raw.githubusercontent.com/Samrux/Pac-Man-Bot/master/_Resources/Avatar.png)
