# Trapline

A mod for the Steam game *Survival Log* that collects the prey of all your traps with one click. In the HUD trap list, a "Take All" button (the game's own word) shows in the header row when at least one trap has prey. A click queues the game's normal pickup action for each trap that has prey: the character walks to each trap and each pickup takes its normal game time, the same as clicking a trap by hand, opening its panel, and pressing collect. Actions already queued run before the pickups. A second click before the route finishes adds no duplicate pickup.

The mod plans the order of the traps to keep the walk short. It clears your current floor first, then visits other floors in order of distance from the floor you were on when you clicked. On each floor, it goes to the nearest trap next.

If the bag fills up during the route, the game shows its own "Inventory is full." message and the prey stays in that trap. The mod then removes the rest of the route from the action queue, so the character does not walk on to the next trap. Anything the player queued separately stays in the queue.

The open trap list is also a compact grid, with one line for each floor. The line starts with the floor name. After it comes one cell for each trap slot the player can use right now, whether it holds a trap or is free: a cell with a trap shows the trap icon, and a free slot is an empty cell. A trap with prey has a gold border and a gold dot. Each trap keeps its cell while the usable slots on that floor stay the same. A line has at most 6 cells, and more cells continue on the next line. A floor with free slots and no trap also shows its line. A click on a trap cell does what a click on that trap in the game's own list does. A click on an empty cell does nothing.

Trapline does not rebait a trap after collecting it. Rebait by hand, as usual. The mod writes and changes no file of the game install, and it does not change the traps, the prey, or the save format.

## Compatibility

Do not install Trapline together with [TrapPickAll](https://www.nexusmods.com/survivallog/mods/6). Both mods change how trap prey is collected. The difference: TrapPickAll replaces the pickup, and Trapline only automates it. Trapline puts the game's normal pickup action in the queue of the character, one for each trap. The walk, the pickup time, and the rewards are the same as when you collect by hand.

## Requirements

The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Extract this mod's zip into the game folder (the folder with the game .exe). The DLL lands in `BepInEx\plugins`. Full path example:
   - Steam: `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\Trapline.dll`

## Uninstall

Delete `Trapline.dll` from the `BepInEx\plugins` folder. The HUD trap list looks and works as without the mod on the next start.

## Troubleshooting

`BepInEx\config\com.ivmakk.survivallog.trapline.cfg` has one entry, `General` / `Verbose` (default off), which turns on debug logging of the route and the page script. To write these debug entries to the log file, also add `Debug` to `LogLevels` under `[Logging.Disk]` in `BepInEx\config\BepInEx.cfg`. If a game update changes the HUD trap list, Trapline turns off only the feature that needs the missing part and keeps the rest working. Look in `BepInEx\LogOutput.log` for the `Trapline loaded.` line and for a warning or an error from Trapline.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. Those assemblies are game-derived and are not part of this repo. The .NET 8 SDK is required.

```
dotnet build src/Trapline.csproj -c Release
```

`Directory.Build.props` sets `GameDir` to the default Steam install path. If the game is in another place, override it without an edit of the file: set a `GameDir` environment variable, or pass `-p:GameDir=...` on the build. The output DLL is at `src\bin\Release\Trapline.dll`.

The route order and the floor slot layout are game-free code (`src/RouteLogic.cs`, `src/SlotLogic.cs`) with unit tests. The tests do not need the game:

```
dotnet test tests/Trapline.Tests
```

The page script `src/page.js` has its own test, which runs it against the real `CoreUI1.html` of the installed game (Node with jsdom). Run it after a game update. It needs the game install, and `SL_GAME_DIR` overrides the default Steam path:

```
cd tests/page
npm install
npm test
```

## Package

Add `-p:Package=true` to a Release build to also write the ready-to-install zip at `dist\Trapline-<version>.zip`, laid out as `BepInEx\plugins\Trapline.dll` so a user extracts it at the game root. A plain build skips this step.

```
dotnet build src/Trapline.csproj -c Release -p:Package=true
```

## License

Licensed under the GNU General Public License v3.0. Copyright (C) 2026 ivmakk. See [LICENSE](LICENSE).

You may reuse and modify this mod, but you must keep it open under the same license and give credit. Do not reupload it without credit.
