# StoreBot

[![GitHub stars](https://img.shields.io/github/stars/StoreDev/StoreBot?style=social)](https://github.com/StoreDev/StoreBot)
[![GitHub Workflow - Docker](https://img.shields.io/github/workflow/status/StoreDev/StoreBot/docker?label=docker)](https://github.com/StoreDev/StoreBot/actions?query=workflow%3Adocker)
[![GitHub Workflow - Build](https://img.shields.io/github/workflow/status/StoreDev/StoreBot/build?label=build)](https://github.com/StoreDev/StoreBot/actions?query=workflow%3Abuild)
[![Docker Pulls](https://img.shields.io/docker/pulls/storedev/store-bot)](https://hub.docker.com/r/storedev/store-bot)
[![GitHub release (latest by date)](https://img.shields.io/github/v/release/storedev/storebot)](https://github.com/StoreDev/StoreBot/releases)
StoreBot is a Discord bot that makes use of [StoreLib](https://github.com/StoreDev/StoreLib). Current features include generation of package urls from a given product listing, conversation of the various Store IDs, and querying details about a listing.

![Advanced Query Command](https://i.imgur.com/sUd7RkM.png)


## Usage:
Clone the repo and build StoreBot using Visual Studio 2019 *Preview*. StoreBot makes use of .NET 5, install the latest (at the time preview) [SDK](https://dotnet.microsoft.com/download/dotnet/5.0). Define your [Discord Bot Token](https://discordapp.com/developers/applications) in your environment variables, named "STOREBOTTOKEN" or edit the `discordtoken` line in config.json.
Run StoreBot.dll using the .NET 5 runtime or build to a exe directly:
```
dotnet StoreBot.dll or StoreBot.exe
```
Once StoreBot logs into Discord, invite the bot to the server of your choice, then refer to the "Commands" section below.


### Commands:
```
@StoreBot help - Lists all commands. Specify a command (Example: `@StoreBot help packages`) to see more information about that command.
```

```
@StoreBot advancedquery ProductIDOrStoreLink environment MARKET-LANG - Returns details about the product from DisplayCatalog (Example: `@StoreBot advancedquery 9wzdncrfj3tj Int US-EN`)
```

```
@StoreBot packages ProductIDOrTitleIDOrStoreLink - Generates and returns FE3 links for app and Xbox Live packages. 
```

```
@StoreBot convert ID IDFormat - Returns alternative store IDs for the given listing (Example: `@StoreBot convert eaac6c6b-10a4-4659-815b-44f151eca61a LegacyWindowsStoreProductID`)
```

```
@StoreBot submittoken MSAOrXBLToken - See [StoreToken](https://github.com/StoreDev/StoreToken) on how to obtain your token. This command can only be used via Direct Message to the bot. Once your token has been submitted, it will be used for future queries (that originate from your user).
```

### Docker instructions:
#### Building the Docker image:
```docker build -t storebot_docker .```
#### Running the Docker image:
```docker run -e storebottoken -it --rm --name storebot storebot_docker```


#### Dependencies
[StoreLib](https://github.com/StoreDev/StoreLib)
[DSharpPlus](https://github.com/DSharpPlus/DSharpPlus)


#### License 
[Mozilla Public License](https://www.mozilla.org/en-US/MPL/)
