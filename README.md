# trashday-mod-settings

One in-game settings screen for every BepInEx mod in Trash Day: a **Mods** tab in the game's Options screen, and F10 to jump straight to it. Packaged for Thunderstore as `darkharasho-TrashDayModSettings`; the store page is `thunderstore/README.md`.

## How it works

- `Patches/OptionsPatches.cs`: after `OptionsUI.OnEnable` rebuilds the Options screen, `ModsTabInstaller` adds a Mods tab button and content and appends a fifth `OptionsUI.Tab` to the game's private tab list, so shoulder-button cycling, navigation and Reset Defaults include it. `OptionsAccess` holds every reflected member; if one is missing, the tab is skipped and Options is untouched.
- `ModsTab` shows a Mod picker and the picked mod's settings grouped by section (advanced last). `SettingsRegistry` reads every plugin's config, honouring ConfigurationManagerAttributes-style tags. `SettingEditors` picks a control per setting type and builds it with the game's own USS classes (`Rows`).
- `ConfigWriter` writes through `BoxedValue` (mods get `SettingChanged` immediately) and saves at most once a second, plus on close.
- `HotkeyController` handles F10 (opening `MidgameUI.OptionsScene` or `MainMenuUI.OptionsScene` on the Mods tab), key rebinding and the save timer.

## Development

- `dotnet test tests/ModSettings.Tests/ModSettings.Tests.csproj` runs the unit tests (pure logic only).
- `dotnet build src/ModSettings/ModSettings.csproj` builds and copies the DLL into the game's `BepInEx/plugins/darkharasho-ModSettings`.
- `scripts/package.sh` builds Release and writes `dist/darkharasho-TrashDayModSettings-<version>.zip`; it fails if the manifest, `Plugin.cs`, csproj and `CHANGELOG.md` versions disagree.
- CI builds against `lib/refs` (from `scripts/update-refs.sh`); pass `-p:UseRefs=true` to try that locally.

### Releases

Bump the version in `thunderstore/manifest.json`, `Plugin.cs`, the csproj and `CHANGELOG.md`, then `git tag vX.Y.Z && git push origin vX.Y.Z`. The workflow tests, packages, creates a GitHub release and publishes to Thunderstore with the `THUNDERSTORE_TOKEN` secret. Thunderstore never allows replacing a published version.

### Paths

Override the game path with `-p:GamePath=...` or a gitignored `Local.props`:

```xml
<Project>
  <PropertyGroup>
    <GamePath>C:\Program Files (x86)\Steam\steamapps\common\TrashDay</GamePath>
  </PropertyGroup>
</Project>
```
