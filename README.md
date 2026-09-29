# Trapline

A mod for the Steam game *Survival Log* that collects the prey of all your traps with one click. In the HUD trap list, a "Take All" button (the game's own word) shows in the header row when at least one trap has prey. A click queues the game's normal pickup action for each trap that has prey: the character walks to each trap and each pickup takes its normal game time, the same as clicking a trap by hand, opening its panel, and pressing collect. Actions already queued run before the pickups. A second click before the route finishes adds no duplicate pickup.

A trap has prey when prey is in the trap, or when prey is in the storage box of an Auto Trap Cage. At an Auto Trap Cage, the pickup is the game's own pickup of the storage box, the same as the game's "Collect All" button in the trap window: the character takes the prey of the storage box and the prey in the cage.

The mod plans the order of the traps to keep the walk short. It clears your current floor first, then visits other floors in order of distance from the floor you were on when you clicked. On each floor, it goes to the nearest trap next.

If the bag fills up during the route, the game shows its own "Inventory is full." message and the prey stays in that trap. The mod then removes the rest of the route from the action queue, so the character does not walk on to the next trap. Anything the player queued separately stays in the queue. At an Auto Trap Cage, the bag can have room for only a part of the prey: the character takes what fits, the rest stays in the storage box, and the route stops there too.

The open trap list is also a compact grid, with one line for each floor. The line starts with the floor name. After it comes one cell for each trap slot the player can use right now, whether it holds a trap or is free: a cell with a trap shows the trap icon, and a free slot is an empty cell. A trap with prey has a gold border and a gold dot. A Stopped Auto Trap Cage (its storage box is full, so it catches nothing more) has a red border and a red dot. When a trap holds more than one prey, its cell shows the game's count (for example ×4) as a small badge in the corner. Each trap keeps its cell while the usable slots on that floor stay the same. A line has at most 6 cells, and more cells continue on the next line. A floor with free slots and no trap also shows its line. A click on a trap cell does what a click on that trap in the game's own list does. A click on an empty cell does nothing.

Trapline does not rebait a trap after collecting it. Rebait by hand, as usual. The mod writes and changes no file of the game install, and it does not change the traps, the prey, or the save format.

Nexus page: https://www.nexusmods.com/survivallog/mods/15

## Compatibility

Do not install Trapline together with [TrapPickAll](https://www.nexusmods.com/survivallog/mods/6). Both mods change how trap prey is collected. The difference: TrapPickAll replaces the pickup, and Trapline only automates it. Trapline puts the game's normal pickup action in the queue of the character, one for each trap. The walk, the pickup time, and the rewards are the same as when you collect by hand.

## Requirements

- Survival Log 1.1.18153 (the Autumn Update) or later.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Extract this mod's zip into the game folder (the folder with the game .exe). The DLL lands in `BepInEx\plugins`. Full path example:
   - Steam: `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\Trapline.dll`

## Uninstall

Delete `Trapline.dll` from the `BepInEx\plugins` folder. The HUD trap list looks and works as without the mod on the next start.

## Troubleshooting

`BepInEx\config\com.ivmakk.survivallog.trapline.cfg` has one entry, `General` / `Verbose` (default off), which turns on debug logging of the route and the page script. To write these debug entries to the log file, also add `Debug` to `LogLevels` under `[Logging.Disk]` in `BepInEx\config\BepInEx.cfg`. If a game update changes the HUD trap list, Trapline turns off only the feature that needs the missing part and keeps the rest working. Look in `BepInEx\LogOutput.log` for the `Trapline loaded.` line and for a warning or an error from Trapline.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against the game's IL2CPP interop assemblies. The build needs an installed copy of the game with BepInEx. Start the game once with BepInEx to generate the assemblies. This repo does not include those assemblies.

The build needs the .NET 8 SDK and Node 22. The HUD script uses TypeScript in `src/Web/page/`. Vite builds it into one file, which the DLL embeds. For [mise](https://mise.jdx.dev) users, `mise.toml` specifies Node 22.

Run these commands from the mod root:

1. If you use mise, run `mise trust` once after cloning the repo.
2. Install the npm packages from the lock file with `npm ci`.
3. Build the mod with `dotnet build src/Trapline.csproj -c Release`.

The build runs Vite when a page source file changes. If the npm packages are missing, the build stops with a message that explains how to install them.

`Directory.Build.props` sets `GameDir` to the default Steam install path. For another location, set the `GameDir` environment variable or pass `-p:GameDir=...` to the build command. The output DLL is at `src\bin\Release\Trapline.dll`.

These source files contain logic that does not need the game:

- `src/TakeAll/RouteLogic.cs` calculates the route order.
- `src/Grid/GridLogic.cs` calculates the floor slot layout.
- `src/Web/PageJson.cs` prepares data for the web page.
- `src/Web/PushSchedule.cs` controls when the mod sends data.

Run their unit tests with this command. The tests do not need the game.

```
dotnet test tests/Trapline.Tests
```

The page tests use Vitest with jsdom. They run the built script against `CoreUI1.html` from the installed game. Run them after a game update. Set `SL_GAME_DIR` if the game is outside the default Steam path.

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
