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

- **Pure logic is separated and tested.** Logic that does not need the running game (the route order, the floor slot layout) lives in its own file with no BepInEx or Il2Cpp reference, unit-tested under `tests/`. Flag new pure logic in `Plugin.cs`, and new pure logic with no test.
- **Patches** prefer a postfix, and tie the `Harmony` instance to the plugin GUID.
- **Normal game actions only.** The mod queues the game's own pickup action (`TrapManager.OnStartPickupTrapPrey`) and removes only the actions its route added. Flag code that gives prey, EXP, or items directly, makes a pickup faster, or removes an action the player queued.
- **Page script.** `src/page.js` runs in the web view. It writes to the DOM only when a value differs (a MutationObserver watches the page), and it turns off a feature whose page part is missing. `tests/page` runs it against the game's own `CoreUI1.html`.

## Release and config hygiene

- **Verbose ships off.** The `Verbose` config binds with default `false`. Diagnostic tracing goes on `LogDebug` behind it; `LogInfo` stays quiet apart from the load line.
- **The plugin GUID never changes.** It is `com.ivmakk.survivallog.trapline`, the BepInEx identity and the config file name. Flag any edit to it.
- **The version is in two places that must agree:** `<Version>` in the csproj and the `BepInPlugin` attribute.
- **No committed build output.** Flag `bin/`, `obj/`, `dist/`, or a game DLL in the diff. Game `<Reference>` entries keep `<Private>false</Private>`.
- **Changelog matches the change.** A player-visible change adds an `[Unreleased]` entry to `CHANGELOG.md` in player-facing wording. An internal-only refactor gets none.
