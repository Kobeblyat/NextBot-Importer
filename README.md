# Garry's Mod NextBot Importer

A bilingual Windows desktop application made by Bilibili creator
[Kobeblyat](https://space.bilibili.com/3546897006463032).

It converts images and audio into playable Garry's Mod NextBot addons and exports
them directly to `GarrysMod/garrysmod/addons`.

> Please use this tool responsibly. Do not flood the Steam Workshop with large
> numbers of low-effort NextBots.

## Features

- One executable with Chinese and English interfaces
- Automatically selects the initial language from the Windows display language
- Runtime language switching
- Remembers the selected language for the next launch
- Custom in-app responsible-use notice
- PNG, GIF, JPG/JPEG, BMP, and TIF/TIFF input
- Preserves GIF animation frames and timing
- Fit-entire-image and fill/crop modes
- MP3/WAV chase, death, and jump audio
- Custom NPC spawn-menu categories
- Self-contained Windows x64 build

## Requirements

- Windows 10 or Windows 11
- .NET 9 SDK for building from source

## Project layout

```text
NextbotImporter/        Application and addon generation code
iconimage.ico           Application icon
```

## Build

Run from the repository root:

```powershell
.\build.ps1
```

Or publish manually:

```powershell
dotnet publish .\NextbotImporter\NextbotImporter.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false `
  -o .\dist
```

## Garry's Mod note

NextBots require a Nav Mesh. If a map does not have one, run `nav_generate` in
the game console. Generation can take a long time and may restart the map.

## Author

Kobeblyat  
https://space.bilibili.com/3546897006463032
