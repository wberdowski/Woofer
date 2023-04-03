# Woofer - Discord Music Bot

Woofer is a self-hosted Discord music bot built using [Discord.Net](https://github.com/discord-net/Discord.Net) and running on .NET 6.0. The bot supports high-quality audio playback from popular platforms like YouTube, Soundcloud (coming soon), and Spotify (coming soon).

> **Warning** Please note that this project is currently in an ***early stage of development (alpha)***. While the bot is functional and has been tested, it may contain bugs or unexpected behavior. If you encounter any issues or bugs, please report them under the [Issues](https://github.com/wberdowski/Woofer/issues) tab in the project repository.

## Features
✔️ Self-hosted for complete control and privacy

✔️ Runs both on Windows and Linux platforms

✔️ High-quality audio playback from YouTube

> **Note** More features coming very soon...

## Installation

- Download the .NET Runtime 6.0.* from the [official Microsoft website](https://dotnet.microsoft.com/download/dotnet/6.0).
- Download the latest release of Woofer from [Releases tab](https://github.com/wberdowski/Woofer/releases), and unzip it.

- Windows
    - Run ``Woofer.Core.exe``.

- Linux
    - Install packages required for audio streaming and encryption ``sudo apt-get install libopus-dev libsodium-dev``
    - Run ``/usr/bin/dotnet Woofer.Core``.

- Open the ``config.json`` file and insert your bot token obtained at [Discord Developer Portal](https://discord.com/developers/applications).

## Usage

Once Woofer is up and running, you can use various slash commands. To see the full list of available commands, use the `/help` command. You can type it either in the text channel that Woofer has access to, or message directly to the bot.

## Status

|Branch|Status|
|---|---|
|Main|![Build](https://github.com/wberdowski/Woofer/actions/workflows/build.yml/badge.svg?branch=main)|
|Develop|![Build](https://github.com/wberdowski/Woofer/actions/workflows/build.yml/badge.svg?branch=develop)|

## Contributions

Contributions to Woofer are welcome and encouraged! If you find any issues or have ideas for new features, feel free to open an issue with the appropriate label or submit a pull request.
