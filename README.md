# Classy Sentinel

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
- Deliberate controller focus supports D-pad/left-stick navigation, a separate
  cyan focus indicator, default equip, alternate gear-set selection, and exit.
- Controller input is not intercepted merely because the panel is visible.
- New or temporarily unknown jobs remain visible under **Other / New Jobs**.
- Preferences are keyed by stable ClassJob row IDs, not screen position.

## Commands

- `/classysentinel` or `/csentinel` — toggle the unified panel
- `/csentinel focus` / `unfocus` — enter or leave controller focus
- `/classysentinel config` — open settings
- `/classysentinel show` / `hide`
- `/classysentinel lock` / `unlock`
- `/classysentinel refresh`

## Controller controls

Activate controller focus with the panel button or `/csentinel focus`. An
optional L1 + R1 activation shortcut can be enabled in settings and is off by
default.

- D-pad or left stick: move through the visible grid
- A / Cross: equip the focused job's default gear set
- X / Square: open alternate gear sets
- B / Circle: close the picker or exit controller focus
- Y / Triangle in the picker: set the highlighted gear set as default

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
