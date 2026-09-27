# Copilot code-review instructions

This repo is a BepInEx 6 (IL2CPP) Harmony mod for *Survival Log*. Plugins derive from `BasePlugin` and call the game through Il2CppInterop proxy assemblies. Review with these traps in mind; a general C# review misses most of them.

## IL2CPP Harmony traps

- **Getter/setter patches often never fire.** il2cpp inlines trivial accessors, so a `MethodType.Getter`/`.Setter` patch silently does nothing. Flag a new getter patch used as the only mechanism. The reliable change is mutating the backing field at a load hook.
- **No `is`/`as` across the interop boundary.** Flag `is`, `as`, or a direct cast on a game type. The correct form is `x.TryCast<T>()` then a null check.
- **No `foreach` over game collections.** The interop enumerator lacks the pattern. Expect `GetEnumerator()` / `MoveNext()` / `Current`, or a count plus an indexer.
- **Never read an `Il2CppSystem.ValueTuple<...>` result of a game method,** direct or as a list element. The interop layer reads the fields wrongly and gives garbage with no error. Expect a method that returns a class, a dictionary, or an `Il2CppStructArray`, or the value calculated in the mod.
- **Guard game lookups.** Singletons and config lookups return null often. Flag an unchecked dereference inside a patch.
- **A patch must not break the game.** Each patch body sits in a try/catch that logs the error, so the HUD and the action queue fall back to the game's own behavior.

## Structure and tests

- **Feature folders.** `src/Plugin.cs` holds only the config, the patch list, and `FramePatch` (the one per-frame hook). Each feature has its folder: `src/TakeAll/` (the button's route) and `src/Grid/` (the trap grid), with its patch classes named `<Feature>On<Target>`. `src/Web/` holds the page code and the C# that sends it. Each patch class is attached on its own in the patch list of `Load`. Flag a new patch class missing from that list.
- **Pure logic is separated and tested.** Logic that does not need the running game (the route order, the floor slot layout, the JSON for the page, the send schedule) lives in its own file with no BepInEx or Il2Cpp reference, unit-tested under `tests/`. Flag new pure logic in a patch or game-facing file, and new pure logic with no test.
- **Patches** prefer a postfix, and tie the `Harmony` instance to the plugin GUID.
- **Normal game actions only.** The mod queues the game's own pickup action (`TrapManager.OnStartPickupTrapPrey`) and removes only the actions its route added. Flag code that gives prey, EXP, or items directly, makes a pickup faster, or removes an action the player queued.
- **Page code.** TypeScript modules under `src/Web/page/` (`main.ts` is the entry), built by Vite into one IIFE file that the csproj embeds. The Node project (`package.json`, `package-lock.json`, `vite.config.ts`, `tsconfig.json`) is at the repo root and the build needs it: `npm ci`, then `dotnet build`. The script writes to the DOM only when a value differs (a MutationObserver watches the page), and it turns off a feature whose page part is missing. `tests/page` runs the bundle against the game's own `CoreUI1.html` (Vitest), and `npm run typecheck` runs `tsc --noEmit`.
- **Send once.** The page script goes to the web view only when the page answers `'no script'`. Each other send is `setData` with the button word and the floor groups, only when they changed, or `check()` about once each second. The page script has no retry loop: `PushSchedule` in C# is the only retry, at most once each second. Flag a send of the full script on each push, a push of unchanged data, or a `setTimeout` retry in the page code.
- **Styles in CSS files.** The rules are in `src/Web/page.css` and use the `--tl-` tokens of `src/Web/tokens.css`, never a raw color (`npm run lint`). The script sets only run-time values, as the custom properties `--tl-row`, `--tl-col`, and `--tl-cols`. Flag style text or a raw color in the page code.

## Release and config hygiene

- **Verbose ships off.** The `Verbose` config binds with default `false`. Diagnostic tracing goes on `LogDebug` behind it; `LogInfo` stays quiet apart from the load line.
- **The plugin GUID never changes.** It is `com.ivmakk.survivallog.trapline`, the BepInEx identity and the config file name. Flag any edit to it.
- **The version is in two places that must agree:** `<Version>` in the csproj and the `BepInPlugin` attribute.
- **No committed build output.** Flag `bin/`, `obj/`, `dist/`, `node_modules/`, a built page script, or a game DLL in the diff. Game `<Reference>` entries keep `<Private>false</Private>`.
- **Changelog matches the change.** A player-visible change adds an `[Unreleased]` entry to `CHANGELOG.md` in player-facing wording. An internal-only refactor gets none.
