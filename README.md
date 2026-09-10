# Classy Sentinel

<p align="center">
  <img src="assets/icon.png" alt="Classy Sentinel crest" width="256" height="256">
</p>

Classy Sentinel is a standalone Dalamud plugin that turns the player's existing
FFXIV gear sets into a polished, temporary job-switching launcher.

## MVP features

- Discovers gear sets directly from the live client.
- Groups jobs from current `ClassJob` game data, including first-class Limited
  Jobs support for Blue Mage, Beastmaster, and future limited jobs.
- Loads native job icons from FFXIV assets at runtime. No Square Enix job art is
  included in this repository or package.
- The panel stays completely hidden during normal gameplay. R3 opens a temporary
  selector; Cross equips the highlighted set and immediately closes it.
- Every tile represents one exact FFXIV gear set, so multiple sets for the same
  job can appear and be navigated independently.
- One default set per job is discovered automatically. Additional sets are
  explicitly enabled in settings so large saved-set lists do not create clutter.
- Left-click equips the tile's exact gear set. Right-click can equip it, promote
  it to the job default, or remove it from the launcher.
- The unified panel preserves visual role sections while moving, locking,
  scaling, and wrapping as a single unit.
- Categories and exact gear-set entries can be hidden; remaining buttons reflow
  dynamically with no reserved gaps.
- Duplicate job icons receive a small gear-set number badge, while the tooltip
  shows the full FFXIV gear-set name, number, and item level.
- R3 selection supports D-pad navigation, a separate cyan selection indicator,
  one exact equip request, and immediate exit.
- Controller input is not intercepted merely because the panel is visible.
- Includes an official 512x512 transparent Classy Sentinel crest; FFXIV job
  icons continue to load from game assets at runtime.
- New or temporarily unknown jobs remain visible under **Other / New Jobs**.
- Saved launcher entries use gear-set slot, ClassJob ID, and gear-set name
  together. Renamed, deleted, moved, or reused slots never silently redirect to
  a different set and remain removable in settings.

## Commands

- `/classysentinel` or `/csentinel` — temporarily open or close the launcher
- `/classysentinel config` — open settings
- `/classysentinel show` / `hide`
- `/classysentinel lock` / `unlock`
- `/classysentinel refresh`

## Controller controls

Press R3 during normal gameplay to show the launcher and begin selection. It
does not use Dalamud's global ImGui gamepad-navigation mode.

- R3: open or cancel selection
- D-pad: move through the visible grid
- X / Cross: equip the exact selected gear set, then hide the launcher
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
