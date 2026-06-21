# Ratropolis Performance Mod

An unofficial BepInEx mod for severe late-game slowdown caused by very large
friendly armies. It was developed and tested with a save containing more than
2,000 units.

## What It Does

- Replaces thousands of friendly `AttackRange` Physics2D triggers with a
  centralized range scanner.
- Disables unnecessary friendly-to-friendly body contacts.
- Batches Physics2D transform synchronization after the game's movement pass.
- Adds Crowd Display modes for reducing rendered army size.
- Prevents hidden units from creating thousands of particles, floating text,
  and buff-icon objects.
- Keeps every real unit active for combat, stats, damage, buffs, movement, and
  saving. Crowd Display changes presentation only.

## Full Comparison Video

[Watch the complete side-by-side test with more than 2,000 units on YouTube](https://youtu.be/7Ug2rzYSh2c).

## Download and Install

1. Open
   [GitHub Releases](https://github.com/21twoone/RatropolisPerformanceMod/releases).
2. Download `RatropolisPerformanceMod-v1.1.0-win-x86.zip`.
   Do not download GitHub's automatic `Source code` archives.
3. In Steam, right-click **Ratropolis**, then select
   **Manage > Browse local files**.
4. Close Ratropolis.
5. Extract the zip directly into the Ratropolis game folder and allow folders
   to merge.
6. Start the game. The top-left HUD should show the optimizer and Crowd mode.

Use `RatropolisPerformanceMod-v1.1.0-plugin-only.zip` instead if BepInEx
5.4.23.4 x86 is already installed.

To upgrade from v1.0.0, close the game and extract v1.1.0 over the existing
installation.

## Controls

| Key | Function |
| --- | --- |
| `F6` | Toggle the core AttackRange and Physics2D optimizations. |
| `F7` | Cycle `Crowd ULTRA -> Crowd 1:N -> Crowd OFF -> ULTRA`. |
| `F8` | Increase the Crowd ratio by 10. |
| `F9` | Decrease the Crowd ratio by 10, with a minimum of 1:10. |

### Crowd Modes

- **ULTRA**: shows one representative of each friendly unit type. Recommended
  for armies with thousands of units.
- **1:N**: shows one representative for every N units while always keeping at
  least one of each unit type visible.
- **OFF**: restores all unit bodies and visual effects.

New installations start in `ULTRA`. The selected mode and ratio are saved in:

```text
BepInEx\config\local.ratropolis.performance.cfg
```

## Recommended Settings

- Keep `Optimizer ON`.
- Use `Crowd ULTRA` for maximum performance and lower visual-object memory use.
- Use `F8` and `F9` to choose a denser ratio when you want to see more units.
- Avoid `Crowd OFF` with extremely large armies unless full visuals are needed.

## Limitations

Ratropolis is a 32-bit game. The mod reduces rendering, Physics2D work, and
visual-object pool growth, but it cannot remove the real unit simulation or
raise the game's address-space limit.

Large saves can still pause briefly during the game's synchronous autosave,
because the game serializes every unit and buff on the main thread.

The mod does not edit or convert save files. Back up important saves before
installing any game mod.

## Uninstall

Close the game and run `uninstall-mod.ps1`, or delete:

```text
BepInEx\plugins\RatropolisPerformanceMod.dll
```

Removing BepInEx itself is optional.

## Build from Source

Requirements:

- .NET SDK capable of targeting .NET Framework 3.5
- A local Ratropolis installation
- BepInEx 5.4.23.4 x86 extracted under
  `deps\BepInEx_win_x86_5.4.23.4`

PowerShell:

```powershell
$env:RATROPOLIS_DIR = 'C:\path\to\Steam\steamapps\common\Ratropolis'
dotnet build -c Release
```

The project references local game assemblies only for compilation. No
Ratropolis files are committed or distributed.

## Compatibility

- Ratropolis Steam App ID `1108370`
- Unity Mono x86
- BepInEx `5.4.23.4` x86

This project is not affiliated with Cassel Games.

## License

Source code is available under the [MIT License](LICENSE).
