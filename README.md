# Classy Sentinel

<p align="center">
  <img src="assets/icon.png" alt="Classy Sentinel crest" width="256" height="256">
</p>

Classy Sentinel is a standalone Dalamud plugin that turns the player's existing
FFXIV gear sets into one polished, movable job-switching panel.

## MVP features

- Discovers gear sets directly from the live client.
- Groups jobs from current `ClassJob` game data, including first-class Limited
  Jobs support for Blue Mage, Beastmaster, and future limited jobs.
- Loads native job icons from FFXIV assets at runtime. No Square Enix job art is
  included in this repository or package.
- Left-click equips the default gear set for a job.
- Right-click chooses another set or changes the saved default.
- One unified panel preserves visual role sections while moving, locking,
  scaling, hiding, and wrapping as a single unit.
- Categories and individual jobs can be hidden; remaining buttons reflow with
  no reserved gaps.
- A temporary R3 controller selector supports D-pad navigation, a separate cyan
  selection indicator, single-request default equip, and immediate exit.
- Controller input is not intercepted merely because the panel is visible.
- Includes an official 512x512 transparent Classy Sentinel crest; FFXIV job
  icons continue to load from game assets at runtime.
- New or temporarily unknown jobs remain visible under **Other / New Jobs**.
- Preferences are keyed by stable ClassJob row IDs, not screen position.

## Commands

- `/classysentinel` or `/csentinel` — toggle the unified panel
- `/classysentinel config` — open settings
- `/classysentinel show` / `hide`
- `/classysentinel lock` / `unlock`
- `/classysentinel refresh`

## Controller controls

With the unified panel visible, press R3 to open or cancel the temporary job
selector. It does not use Dalamud's global ImGui gamepad-navigation mode.

- R3: open or cancel selection
- D-pad: move through the visible grid
- X / Cross: equip the selected job's default gear set, then exit
- Circle: cancel selection

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
