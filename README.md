# Portrait Specification Framework

A mod for Elin that assigns character portraits automatically from pools you define: by character type, gender, race, job, character id and age range. Pick a portrait for one character, reroll a whole map, or link portrait packs to the characters they were drawn for.

## Requirements

- Elin EA 23.352 or newer
- [YK Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3400020753) from the Steam Workshop

## Install

Copy the `Mod_PortraitSpecificationFramework` folder into `Elin/Package` and enable it in the in-game mod list. The first launch creates `Package/Mod_Specific_Portraits_Settings`, a small package that stores your pool links so they survive updates of this mod. Keep it enabled.

## Use

Open the tools menu in game (the wrench on the menu bar), then **mod > Portrait Tools**.

- **Portrait Picker**: click a character on the left and a portrait on the right to assign it. A portrait you pick is locked: map rerolls and zone changes leave it alone. *Reset Portrait* removes the portrait and locks the character too. *Lock / Unlock Selection* toggles the lock. *Reroll Selection* unlocks and draws a new portrait from the matching pools. *Reroll Entire Map* rerolls everyone except locked characters, party members and pets. The search fields filter both lists as you type.
- **Portrait Pool Links**: choose a package in the mod filter, select portraits, set the filters (type, gender, race, id, job, age) and press *Add selection to pool*. *Preview matches on this map* lists the characters the current filter would apply to. Selected portraits list the pools they already belong to; click a pool to remove them from it. *Save pools to link file* writes the pools to disk. Closing the window with unsaved edits asks whether to save them.
- **Settings**: the reroll line in dialogue, whether default portraits are mixed into pools, and a random portrait for characters nothing matches. The same settings appear in the Mod Options menu when that mod is installed.

Pool types: `c` for ordinary people, `guard`, `foxfolk`, `special`, or `all`. A pool made for a specific character id wins over broader pools. Age limits are inclusive; leave them at 0 for no limit.

## Link files

Pools are stored in `PSFLinks.json` files. Portrait packs can ship one next to their `package.xml` so their portraits land on the right characters out of the box. Pools you make for the game's own portraits or for workshop packs are saved into the settings package instead, so a workshop update cannot wipe them.

## Build

Needs the .NET SDK, an Elin install and YK Framework from the workshop.

```
./build.ps1
./build.ps1 -ElinDir "D:\Games\Elin" -WorkshopDir "D:\Games\workshop\content\2135150"
./build.ps1 -NoInstall
```

The script builds `SpecificPortraits.dll` against the game's assemblies and installs it with the package files into `Elin/Package/Mod_PortraitSpecificationFramework`. The game location comes from the parameters, else from the `ELIN_DIR` and `ELIN_WORKSHOP_DIR` environment variables, else the default Steam install. The same paths can be passed to `dotnet build` as `-p:ElinDir=...` and `-p:WorkshopDir=...`.
