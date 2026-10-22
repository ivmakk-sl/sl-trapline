# Trapline

A mod for the Steam game *Survival Log*. Checking traps by hand means a click, a walk, and a panel for each trap. Trapline does it in one click and shows all your traps in a compact grid. Walks, action times, and rewards stay the same as by hand.

- **Take All.** One click collects every trap with prey, also Auto Trap Cages, along the shortest walk: your floor first, then the next one by the stairs.
- **Trap grid.** Your traps and free Trap Slots by floor and zone. Gold marks prey, and red marks a trap that breaks at its next pickup. Point at a cell for the game's trap card, and pin the list to keep it open.
- **Set and replace traps.** Click + on a free slot to set a trap from your backpack or home storage. Auto-replace (on by default) sets a new trap of the same kind when one breaks.

Trapline does not rebait traps. The Auto-replace settings are small values in your save, which the game ignores if you remove the mod.

Nexus page: https://www.nexusmods.com/survivallog/mods/15

## Details

In the HUD trap list, a "Take All" button (the game's own word) shows in the header row when at least one trap has prey. A click queues the game's normal pickup action for each trap that has prey: the character walks to each trap and each pickup takes its normal game time, the same as clicking a trap by hand, opening its panel, and pressing collect. Actions already queued run before the pickups. A second click before the route finishes adds no duplicate pickup.

A trap has prey when prey is in the trap, or when prey is in the storage box of an Auto Trap Cage. At an Auto Trap Cage, the pickup is the game's own pickup of the storage box, the same as the game's "Collect All" button in the trap window: the character takes the prey of the storage box and the prey in the cage.

The mod plans the order of the traps for the shortest walk. It clears your current floor first, then goes on to the floor that the stairs reach first from there, and so on. On each floor it picks the order with the shortest walk through all traps with prey, measured along the paths your character really walks, around walls and furniture. The kind of trap and its prey do not change the order.

If the bag fills up during the route, the game shows its own "Inventory is full." message and the prey stays in that trap. The mod then removes the rest of the route from the action queue, so the character does not walk on to the next trap. Anything the player queued separately stays in the queue. At an Auto Trap Cage, the bag can have room for only a part of the prey: the character takes what fits, the rest stays in the storage box, and the route stops there too.

The open trap list is also a compact grid. Each floor has a line with its name, and under it one cell for each trap slot you can use right now, whether it holds a trap or is free: a cell with a trap shows the trap icon, and a free slot is an empty cell. The zone of a trap slot changes what the trap catches, so a floor with two zones also shows a line with the name of its second zone over the cells of that zone: Outdoors on the Home floor of the Warehouse Manager (under Warehouse Interior), and Balcony on the 2nd Floor of the Wage Slave home. A floor with one zone shows only the floor name. A trap with prey has a gold border and a gold dot. A Stopped Auto Trap Cage (its storage box is full, so it catches nothing more) has a red border and a red dot. When a trap holds more than one prey, its cell shows the game's count (for example ×4) as a small badge in the corner. Each trap keeps its cell while the usable slots of its zone stay the same. A line has at most 8 cells, and more cells continue on the next line. A click on a trap cell does what a click on that trap in the game's own list does.

Point at a trap cell to see the game's trap card next to it: the trap name, the durability, the bait, and the prey, as when you point at the trap in the world. When the next pickup will break a trap, its cell turns red: a red border and a red tint. The pickups left count the durability that one pickup takes at your trapping level.

Click an empty cell to set a trap there. A menu opens under the cell with each kind of trap that your backpack and home storage hold, with its icon and count. Pick one: your character walks to that Trap Slot and sets the trap with the game's normal action and time, with a trap from the backpack first, else from home storage. With no trap, the menu shows "No Traps Available".

Trapline replaces each trap that breaks. Right after the pickup that broke it, your character sets a new trap of the same kind on the same Trap Slot, with the game's normal action, before the next pickup of a Take All route. The trap comes from the backpack first, else from home storage. With no trap of that kind, the Trap Slot stays free; the mod never sets another kind. Auto-replace is on by default: clear the "Auto-replace" checkbox on the last line of the open list to turn it off. Each Trap Slot also has its own "Auto-replace" checkbox in the header of its trap window, on by default, to leave that slot out.

The pin at the right end of the last line keeps the list open: a pinned list stays open when you open the plant list or the search, or press Escape. Click the arrow of the list header to unpin and close it. The pin stays after a restart and for each save.

The mod writes and changes no file of the game install, and it does not change the traps or the prey.

## Compatibility

Not compatible:

- [TrapPickAll](https://www.nexusmods.com/survivallog/mods/6): it replaces the game's trap pickup with its own, and Trapline queues the game's normal pickup. Use one of the two.

## Requirements

- Survival Log 1.1.18599 or later.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Extract this mod's zip into the game folder (the folder with the game .exe). The DLL lands in `BepInEx\plugins`. Full path example:
   - Steam: `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\Trapline.dll`

## Uninstall

Delete `Trapline.dll` from the `BepInEx\plugins` folder. The HUD trap list looks and works as without the mod on the next start.

## Troubleshooting

`BepInEx\config\com.ivmakk.survivallog.trapline.cfg` has two entries: `General` / `PinGrid` (default off), which the pin of the trap list sets, and `General` / `Verbose` (default off), which turns on debug logging of the route, the trap grid, and Auto-replace. To write these debug entries to the log file, also add `Debug` to `LogLevels` under `[Logging.Disk]` in `BepInEx\config\BepInEx.cfg`. If a game update changes the HUD trap list, Trapline turns off only the feature that needs the missing part and keeps the rest working. Look in `BepInEx\LogOutput.log` for the `Trapline loaded.` line and for a warning or an error from Trapline.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against the game's IL2CPP interop assemblies. The build needs an installed copy of the game with BepInEx. Start the game once with BepInEx to generate the assemblies. This repo does not include those assemblies.

The build needs the .NET 8 SDK and Node 22. The HUD script (`src/Web/page/`) and the trap window script (`src/Web/trappanel/`) use TypeScript. Vite builds each into one file, which the DLL embeds. For [mise](https://mise.jdx.dev) users, `mise.toml` specifies Node 22.

Run these commands from the mod root:

1. If you use mise, run `mise trust` once after cloning the repo.
2. Install the npm packages from the lock file with `npm ci`.
3. Build the mod with `dotnet build src/Trapline.csproj -c Release`.

The build runs Vite when a page source file changes. If the npm packages are missing, the build stops with a message that explains how to install them.

`Directory.Build.props` sets `GameDir` to the default Steam install path. For another location, set the `GameDir` environment variable or pass `-p:GameDir=...` to the build command. The output DLL is at `src\bin\Release\Trapline.dll`.

These source files contain logic that does not need the game:

- `src/TakeAll/RouteLogic.cs` decides which traps go into the route and gives them to the route library, which calculates the route order.
- `src/Grid/GridLogic.cs` lays out the trap slots by floor and zone, and `src/Grid/DurabilityLogic.cs` calculates the pickups left and the durability mark.
- `src/Card/CardLogic.cs` builds the trap card, and `src/SetTrap/MenuLogic.cs` the lines of the Set Trap menu and the trap item to set.
- `src/Replace/ReplaceLogic.cs` decides the Auto-replace of a broken trap, and `src/Replace/SaveKeyLogic.cs` its keys in the save.
- `src/Web/PageJson.cs` prepares data for the web page.
- `src/Web/PushSchedule.cs` controls when the mod sends data.

`src/Shared/` holds copies of shared libraries of the modding workspace: the route order and the walk length (`route`), the JSON text (`json`), the mod texts (`i18n`, with the files in `src/i18n/`), the floor lines and zone lines of the grid (`floor-grid`), the Set Trap menu (`dropdown`), and the pin (`hud-pin`). A copy is written by a sync from the workspace and never edited here; its own tests are in the workspace.

Run their unit tests with this command. The tests do not need the game.

```
dotnet test tests/Trapline.Tests
```

The page tests use Vitest with jsdom. They run the built scripts against `CoreUI1.html` and `TrapPanel.html` from the installed game. Run them after a game update. Set `SL_GAME_DIR` if the game is outside the default Steam path.

Run the page tests, CSS lint, and type check from the mod root:

```
npm test
npm run lint
npm run typecheck
```

Run `npm run dev` to preview the HUD with simulated traps in a browser. The preview reloads when a page file changes. Use it to check appearance and layout. Check behavior in the game.

## Package

Add `-p:Package=true` to a Release build to also write the ready-to-install zip at `dist\Trapline-<version>.zip`, laid out as `BepInEx\plugins\Trapline.dll` so a user extracts it at the game root. A plain build skips this step.

```
dotnet build src/Trapline.csproj -c Release -p:Package=true
```

## License

Licensed under the GNU General Public License v3.0. Copyright (C) 2026 ivmakk. See [LICENSE](LICENSE).

You may reuse and modify this mod, but you must keep it open under the same license and give credit. Do not reupload it without credit.
