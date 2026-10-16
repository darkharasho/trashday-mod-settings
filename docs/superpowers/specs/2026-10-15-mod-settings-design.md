# Trash Day Mod Settings: design

## Goal

One in-game settings screen for every BepInEx mod installed in Trash Day. It's a **Mods** tab added to the game's own Options screen, and F10 jumps straight to it. Players change mod settings without editing config files, and the owning mod gets its usual `SettingChanged` event right away. It's the Trash Day counterpart of `stonewards-mod-settings`, and it follows the build, packaging and release setup of `trashday-more-players` and `trashday-admin-menu`. Packaged for Thunderstore under `darkharasho` as `ModSettings`.

Done means: More Players' lobby cap and the Admin Menu's hotkey can be changed in game, and the changes are still there after a restart.

## Research findings (Trash Day build of 2026-10-13, Unity 6000.3.9f1 Mono; decompiled to /tmp/trashday-src)

### The Options screen (`Trash.OptionsUI`)

- It's a UI Toolkit `UIDocument` in a `MenuScene`. `OnEnable` runs every time the screen opens and rebuilds it:
  - **Tabs:** `InitTab(buttonName, tabName, resetDefaults)` finds a tab button (`GameplayTabBtn`, `GraphicsTabBtn`, `AudioTabBtn`, `KeybindsTabBtn`) and its content element (`GameplayTab`, …) inside `OptionsPanel`'s `content-area`. It returns the private struct `OptionsUI.Tab { Button Button; VisualElement Content; Action ResetDefaults; }`.
  - **Tab list:** the four tabs go into the private `Tab[] _tabs`. Shoulder-button cycling (`PreviousTab` and `NextTab` actions → `CycleTab`) and directional navigation read from it.
  - **Content:** `CreateAllSettings()` clears and refills the Gameplay and Audio content.
  - **Navigation:** for each tab, up from its first focusable element goes to the tab button, and down from its last goes to `ResetDefaultsBtn`.
  - **Opening tab:** `OnEnable` ends with `ShowTab(_gameplayTab)`.
- **Switching tabs:** `ShowTab(Tab)` and `ClickTab(Tab)` toggle the `tab-button--active` and `tab-button-container--active` classes and show the matching content.
- **Reset Defaults:** `OnResetDefaultsClicked` opens a confirmation (`ModalUI.Open(Loc.Localize("Options.ResetDefaultsConfirm")).AddOkButton(_currentTab.ResetDefaults)`), so a tab only has to supply the reset action.
- **Row building:** `AddSlider`, `AddCycle`, `AddDropdown` and `AddButton` build rows with these classes:
  - `setting-row`, a `Trash.SettingRow` element that highlights while focused
  - `options-text`, `setting-label` and `setting-control`
  - `setting-slider` and `slider-value-label`
  - `cycle-control` and `cycle-btn`
  - `setting-dropdown`
  - `rebind-btn`

  Matching those classes gives the game's look.
- **Back:** `BackButton` pops the screen with `MenuSceneStack.PopActiveScene()`.

### Opening the Options screen

- **Title screen:** `MainMenuUI.OptionsScene`.
- **Pause menu:** `MidgameUI.OptionsScene`.
- **How it's opened:** both push the scene with `GetComponentInParent<MenuSceneStack>().PushActiveScene(OptionsScene)`.
- **Menu state:**
  - `MenuSceneManager.MenusSuppressed` is set by the developer console and by the Admin Menu, and blocks menus.
  - `MenuSceneManager.IsMenuOpen` and `LoadingScreenUI.IsVisible` give the menu state.
- **Keys:** the game binds no F10 key.

## Design

### Mods tab (Harmony postfix on `OptionsUI.OnEnable`)

- **Content area:**
  - It's found by name (`ModsTab`) inside `OptionsPanel`'s `content-area`.
  - If it's missing, it's created as a sibling of `AudioTab`, copying that element's class list. So the patch is idempotent across reopenings.
- **Tab button:**
  - It's found by name (`ModsTabBtn`), or created next to `AudioTabBtn` by copying that button's container class list and its own.
  - The label is "Mods".
  - Click and focus behave like the game's own tabs: a click calls `ClickTab`, and focusing it shows the tab.
- **Registering the tab:**
  - A `Tab` value is built through reflection with `ResetDefaults` = reset the selected mod.
  - It's appended to `_tabs` as the fifth tab.
  - Navigation is wired the same way as the game's tabs: first focusable ↔ tab button, last focusable → `ResetDefaultsBtn`.
- **Content refresh:** the tab's content is rebuilt each time `OnEnable` runs, so the mod list reflects the current plugins.
- **Failures:** if any element or private member the patch needs can't be found, it logs one warning and returns. Options stays exactly as the game built it.

### Tab layout

- **Mod picker:**
  - The first row is a "Mod" cycle control in the game's `cycle-control` style. It wraps around at both ends.
  - It lists every plugin that has at least one visible setting, sorted by display name.
  - The selection is remembered for the session.
- **Settings:**
  - Below the picker, the selected mod's settings are grouped under section headings.
  - Sections appear in config order.
  - Within a section, settings are sorted by `Order` (higher first, matching ConfigurationManager), then by name.
- **Advanced settings** are gathered after all sections under an **Advanced** heading.
- **Rows:** each row is a `SettingRow` showing the display name and the control. The description is a smaller line under the name.
- **No settings anywhere:** a single label says "No installed mods have settings."

### Where settings come from

- **Plugins:** every entry in `BepInEx.Bootstrap.Chainloader.PluginInfos` with a live `Instance`.
- **Config files:** the plugin's `Config`, plus any other `ConfigFile` fields declared on the plugin class.
- **Display hints:** read by name from the `ConfigurationManagerAttributes`-style tag object, so mods need no reference to this mod. Supported hints:
  - `Browsable == false` hides the setting.
  - `Category` overrides the section.
  - `DispName` sets the display name.
  - `Order` sorts within a section.
  - `ReadOnly` greys the row out and disables the control.
  - `IsAdvanced` moves the setting to the Advanced group.
  - `Description` overrides the config description.
  - `RestartRequired` shows "restart needed" after a change.

### Controls by setting type

| Setting | Control |
|---|---|
| `bool` | game cycle button, On / Off |
| `AcceptableValueList<T>` or a non-flags enum | game cycle button; game dropdown when there are more than 6 options |
| Number with `AcceptableValueRange<T>` | game slider with its value label; integer types snap to whole numbers |
| Number without a range | text field, validated on Enter or blur; an invalid value reverts to the current one |
| `string` | text field, committed on Enter or blur |
| `KeyboardShortcut` | rebind button: press a key to bind; Esc cancels; Backspace clears to `KeyboardShortcut.Empty` |
| Anything else (colours, vectors, flags enums, …) | read-only text of the value, with "edit in the config file" |

- **Writing values:** every write goes through `ConfigEntryBase.BoxedValue`, so the owning mod's `SettingChanged` fires immediately.
- **Refreshing:** after a write, the rows refresh from the entries, since a mod may clamp or reject a value.
- **Rebinding:**
  - While a rebind is listening, Esc and the game's back and tab-cycle actions are swallowed, so Esc cancels the rebind instead of closing Options.
  - Keys are read with the Input System's keyboard, and modifiers held with the key become the shortcut's modifiers.

### Saving

- `SaveOnConfigSet` is turned off on each touched `ConfigFile` while the tab is in use. Then:
  - Dirty files are saved once a second.
  - They're saved again when the Options screen is disabled.
  - Each file's previous `SaveOnConfigSet` value is then restored.
- **Save failures:** a failed save is logged, and the file is retried on the next tick.

### F10 hotkey

- **Config:** `General.OpenKey` (`KeyboardShortcut`, default F10) is this mod's own setting, so it appears in its own tab.
- **On press:**
  - If an Options screen is already active, it switches to the Mods tab.
  - Otherwise it pushes `MidgameUI.OptionsScene` when in a run, or `MainMenuUI.OptionsScene` on the title screen. It opens on the Mods tab instead of Gameplay.
- **Does nothing when:**
  - a text field has focus
  - a rebind is listening
  - `LoadingScreenUI.IsVisible`
  - `MenuSceneManager.MenusSuppressed` (the developer console or the Admin Menu)
  - no `MainMenuUI` or `MidgameUI` is found
  - a menu other than the pause menu or Options is open (for example a results screen or a modal)
- **Back:** pressing Back returns to wherever the game was. That's gameplay when F10 was pressed in play, since the scene is pushed onto the pause menu's stack without the pause menu itself; manual verification checks this.

### Error handling

- Building a row is wrapped per setting. A failure shows "can't edit here" in that row and logs once per setting.
- Reading a plugin's config is wrapped per plugin. A failure skips that plugin and logs once.
- The postfix and the hotkey are each wrapped, so an exception never breaks the game's Options screen or its update loop.

### Code layout (mirrors trashday-admin-menu)

- **Entry point:** `src/ModSettings/Plugin.cs` holds the config, Harmony, and creates the hotkey controller.
- **`Patches/OptionsPatches.cs`:** the `OnEnable` postfix, which adds the tab and wires it into the private tab list.
- **`OptionsAccess.cs`:** all reflection on `OptionsUI` (the `Tab` struct, `_tabs`, `ClickTab`, `ShowTab`, `_resetDefaultsBtn`), checked once at start-up.
- **`ModsTab.cs`:** builds the picker and the selected mod's rows.
- **`SettingsRegistry.cs`:** collects plugins and settings.
- **`SettingEditors.cs`:** one control per setting type.
- **`KeyRebind.cs`:** listens for a key and builds the shortcut.
- **`ConfigWriter.cs`:** batched saves.
- **`HotkeyController.cs`:** F10 and opening Options.
- **Pure logic, tested by `tests/ModSettings.Tests`:**
  - `EditorKinds.cs` chooses the control per type.
  - `ValueParsing.cs` parses and validates text.
  - `SettingOrder.cs` handles sorting, grouping, the Advanced bucket and visibility filtering.
  - `Picker.cs` handles wrap-around and remembering the selection.
  - `HotkeyGate.cs` decides whether F10 acts.
  - `SaveSchedule.cs` decides when files are saved.

## Testing

- **xunit** (net8.0), compiling the pure files by link. It covers:
  - every control choice in the type table
  - number parsing for each numeric type, including range checks and invalid input
  - integer slider snapping
  - sorting by Order then name, and section order
  - the Advanced bucket and `Browsable=false` filtering
  - empty mods being dropped
  - picker wrap-around, and a remembered selection that has disappeared
  - each F10 gate condition
  - save timing: saves once a second only when dirty, and saves on close
- **Manual checklist** (`docs/manual-verification-0.1.0.md`):
  - the Mods tab from the title screen and the pause menu
  - shoulder-button cycling into and out of Mods
  - controller up and down navigation
  - Reset Defaults, with its confirmation, on the selected mod
  - changing More Players' max players with the slider
  - rebinding the Admin Menu key, with Esc cancelling and not closing Options
  - F10 from gameplay, title and pause, and Back from an F10-opened screen
  - F10 ignored while the Admin Menu is open
  - after a restart, changes persisted

## Out of scope

- Search, and a separate floating window
- Settings for the game itself (they're already in Options)
- Syncing settings between host and clients
- Editors for colours or vectors

## Packaging and release

- **Repo:** `darkharasho/trashday-mod-settings`, BepInEx 5 / netstandard2.1.
- **Package:**
  - The plugin GUID is `com.darkharasho.trashday.modsettings`, version 0.1.0.
  - The DLL is deployed to `BepInEx/plugins/darkharasho-ModSettings`.
  - `scripts/package.sh` builds `dist/darkharasho-ModSettings-<version>.zip`, containing `manifest.json`, a 256×256 `icon.png`, `README.md`, `CHANGELOG.md` and `plugins/ModSettings.dll`.
- **CI:** builds against `lib/refs` generated by `scripts/update-refs.sh`.
- **Release:** `.github/workflows/release.yml` runs on `v*` tags.
  - It checks the versions match and builds the zip.
  - It creates a GitHub release.
  - It runs `scripts/thunderstore-publish.sh <zip> --community trash-day`, with `continue-on-error` until the community exists, as in the sibling mods.
