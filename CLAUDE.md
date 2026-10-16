## Project Context

A BepInEx/Harmony mod for Trash Day (Off by One Games, Steam app 3314580, Unity 6000.3 Mono, UI Toolkit, Input System) that adds a **Mods** tab to the game's Options screen listing every installed BepInEx mod's settings, and an F10 key that opens Options on that tab. Packaged for Thunderstore under `darkharasho` as `ModSettings`.

## Layout

- `src/ModSettings/Plugin.cs`: config, Harmony, creates `HotkeyController`
- `src/ModSettings/Patches/OptionsPatches.cs` + `ModsTabInstaller.cs` + `OptionsAccess.cs`: the tab inside `Trash.OptionsUI` (private members via reflection)
- `src/ModSettings/ModsTab.cs`, `SettingEditors.cs`, `Rows.cs`, `KeyRebind.cs`: the tab's contents
- `src/ModSettings/SettingsRegistry.cs`, `ConfigWriter.cs`: reading and writing BepInEx config
- `src/ModSettings/HotkeyController.cs`: F10, rebinding and save timing
- Pure logic tested by `tests/ModSettings.Tests`: `KeyNames`, `Names`, `EditorKinds`, `ValueParsing`, `SettingOrder`, `Picker`, `HotkeyGate`, `SaveSchedule`
- Release: bump the version in `Plugin.cs`, the csproj, `thunderstore/manifest.json` and `CHANGELOG.md`, then push tag `v<version>`

## Notes

- Decompile with `DOTNET_ROOT=/home/linuxbrew/.linuxbrew/opt/dotnet/libexec ~/.dotnet/tools/ilspycmd -p -o /tmp/trashday-src .../Trash_Data/Managed/Assembly-CSharp.dll`.
- `OptionsUI.OnEnable` runs on every open and rebuilds `_tabs` with the game's four tabs, so the installer re-appends ours and hides it each time.
- Pushing a scene onto an empty `MenuSceneStack` also pushes its default scene underneath (`AutoActivateDefaultScene`), so Back from an F10-opened Options goes to the pause menu or title.
- Thunderstore had no Trash Day community at first release. The workflow's `--community trash-day` is a placeholder; set the real slug and `--categories` once it exists, and remove the step's `continue-on-error`.
- Sibling mods: `trashday-more-players`, `trashday-admin-menu`. Their settings carry `ConfigurationManagerAttributes`, which this mod reads.
