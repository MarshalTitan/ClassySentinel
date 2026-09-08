# Classy Sentinel

Classy Sentinel is a standalone Dalamud plugin that turns the player's existing
FFXIV gear sets into compact, movable role bars.

## MVP features

- Discovers gear sets directly from the live client.
- Groups jobs from current `ClassJob` game data, including first-class Limited
  Jobs support for Blue Mage, Beastmaster, and future limited jobs.
- Loads native job icons from FFXIV assets at runtime. No Square Enix job art is
  included in this repository or package.
- Left-click equips the default gear set for a job.
- Right-click chooses another set or changes the saved default.
- Separate role bars can move independently, lock in place, resize, and wrap.
- New or temporarily unknown jobs remain visible under **Other / New Jobs**.
- Preferences are keyed by stable ClassJob row IDs, not screen position.

## Commands

- `/classysentinel` or `/csentinel` — toggle all role bars
- `/classysentinel config` — open settings
- `/classysentinel show` / `hide`
- `/classysentinel lock` / `unlock`
- `/classysentinel refresh`

## Building

Requirements:

- .NET 10 SDK
- XIVLauncher/Dalamud API 15 development files

```powershell
dotnet restore ClassySentinel.csproj
dotnet build ClassySentinel.csproj -c Release --no-restore
```

The packaged plugin is written to `bin/Release/ClassySentinel/latest.zip`.

## Project boundaries

This repository owns the plugin. The shared Sentinel catalog remains separate;
its `repo.json` should only receive the `ClassySentinel` object for releases.

## AI development disclosure

The initial MVP implementation was created with substantial AI assistance and
must be reviewed and tested in-game by a human maintainer before distribution.

