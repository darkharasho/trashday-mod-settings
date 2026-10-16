# Trash Day Mod Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A BepInEx mod that adds a **Mods** tab to Trash Day's Options screen listing every installed mod's settings, plus an F10 key that opens Options straight on that tab.

**Architecture:**
- **Game-independent logic** is in small pure files compiled straight into an xunit test project: control choice, parsing, ordering, the picker, the hotkey gate, save timing and key names.
- **Game-facing code:**
  - A Harmony postfix on `OptionsUI.OnEnable` inserts a fifth tab into the game's private tab list through reflection, kept in `OptionsAccess`.
  - `ModsTab` builds rows with the game's own USS classes.
  - A `HotkeyController` MonoBehaviour handles F10, key rebinding and batched saves.

**Tech Stack:** BepInEx 5 (Mono) and HarmonyX, on netstandard2.1. Unity 6000.3 with UI Toolkit and the Input System. xunit on net8.0.

**Spec:** `docs/superpowers/specs/2026-10-15-mod-settings-design.md`

## Global Constraints

- **Repo:** `~/Documents/GitHub/trashday-mod-settings`, branch `main`.
  - Commits are signed (1Password); never pass `--no-gpg-sign`.
  - Every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Plugin identity:** GUID `com.darkharasho.trashday.modsettings`, name `ModSettings`, version `0.1.0`.
  - The assembly is `ModSettings.dll`.
  - It deploys to `BepInEx/plugins/darkharasho-ModSettings`.
- **Game path:** `/var/mnt/data/SteamLibrary/steamapps/common/TrashDay`. Managed DLLs are in `Trash_Data/Managed`, and BepInEx is installed in the game folder.
- **Decompiled game source:** `/tmp/trashday-src`. If it's missing, regenerate it with `DOTNET_ROOT=/home/linuxbrew/.linuxbrew/opt/dotnet/libexec ~/.dotnet/tools/ilspycmd -p -o /tmp/trashday-src /var/mnt/data/SteamLibrary/steamapps/common/TrashDay/Trash_Data/Managed/Assembly-CSharp.dll`.
- **Default key:** `General.OpenKey`, a `KeyboardShortcut` defaulting to F10.
- **Names:**
  - Tab button `ModsTabBtn` with text "Mods"; content element `ModsTab`.
  - Empty text: "No installed mods have settings."
  - Advanced heading "Advanced"; restart marker "restart needed".
  - A failed row shows "can't edit here".
  - Read-only suffix: "(edit in the config file)".
- **Cycle vs dropdown:** a cycle button for 6 choices or fewer, a dropdown for more than 6.
- **Saves** are batched: within 1 second of the first change, and on Options close.
- **Tests:** `dotnet test tests/ModSettings.Tests/ModSettings.Tests.csproj`.

## Review Focus

1. **A second open of Options** (title, then pause, or the same screen twice): exactly one Mods button. Mods isn't left visible under the Gameplay tab, and the tab cycle has 5 entries, not 6. `ModsTabInstaller.Install` must stay idempotent and hide the content on each enable.
2. **Esc during a key rebind:** the rebind is cancelled, and Options stays open. That covers both orderings of `KeyRebind.Tick` and the UI cancel event in the same frame (`ConsumesCancel` with its one-frame window).
3. **A setting that throws on write, or a mod that clamps the value:** the row shows the value the entry actually holds afterwards. Every write is followed by a refresh of all rows from the entries.
4. **F10 while a modal (the Reset Defaults confirmation) or a results screen is open:** nothing happens. `OtherMenuOpen` is computed from the game's private list of open stacks.
5. **Unity number formatting in non-English locales:** typed numbers parse with the invariant culture, so "1.5" works on a German system. Pinned by `ValueParsingTests.ParsesWithInvariantCulture`.

---

### Task 1: Pure logic and tests

**Files:**
- Create:
  - `.gitignore`
  - `tests/ModSettings.Tests/ModSettings.Tests.csproj`
  - Under `src/ModSettings/`: `KeyNames.cs`, `Names.cs`, `EditorKinds.cs`, `ValueParsing.cs`, `SettingOrder.cs`, `Picker.cs`, `HotkeyGate.cs`, `SaveSchedule.cs`
- Test: under `tests/ModSettings.Tests/`: `KeyNamesTests.cs`, `NamesTests.cs`, `EditorKindsTests.cs`, `ValueParsingTests.cs`, `SettingOrderTests.cs`, `PickerTests.cs`, `HotkeyGateTests.cs`, `SaveScheduleTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces (all `internal`, namespace `ModSettings`):
  - `KeyNames.ToKeyName(string keyCode) → string`, `KeyNames.ToKeyCodeName(string key) → string`, `KeyNames.IsModifier(string keyCode) → bool`
  - `Names.Humanize(string key) → string`
  - `enum EditorKind { Toggle, Cycle, Dropdown, SliderInt, Slider, NumberText, Text, Key, ReadOnly }`, `enum Acceptable { None, List, Range }`, `EditorKinds.MaxCycleChoices = 6`, `EditorKinds.Choose(Type type, bool isShortcut, Acceptable acceptable, int listCount) → EditorKind`, `EditorKinds.IsInteger(Type)`, `EditorKinds.IsReal(Type)`
  - `ValueParsing.TryParseNumber(string text, Type type, out object value) → bool`, `ValueParsing.InRange(object value, object min, object max) → bool`, `ValueParsing.SnapInteger(double) → double`, `ValueParsing.RoundToStep(float value, float low, float high) → float`, `ValueParsing.Format(object) → string`, `ValueParsing.FormatSlider(float value, bool integer) → string`
  - `struct SettingFacts { string Section; string Name; int Order; bool Advanced; bool Hidden; }`, `sealed class SettingGroup<T> { string Heading; bool IsAdvanced; List<T> Items; }`, `SettingOrder.AdvancedHeading = "Advanced"`, `SettingOrder.Group<T>(IEnumerable<T> items, Func<T, SettingFacts> facts) → List<SettingGroup<T>>`, `SettingOrder.AnyVisible<T>(IEnumerable<T>, Func<T, SettingFacts>) → bool`
  - `Picker.Wrap(int index, int count) → int`, `Picker.IndexOf(IReadOnlyList<string> ids, string remembered) → int`
  - `enum HotkeyAction { None, ShowModsTab, OpenOptions }`, `struct HotkeyState { bool OptionsActive, TextFieldFocused, Rebinding, Loading, MenusSuppressed, HasMenuHost, OtherMenuOpen; }`, `HotkeyGate.Decide(HotkeyState) → HotkeyAction`
  - `sealed class SaveSchedule(float interval = 1f)` with `bool Dirty`, `void MarkDirty(float now)`, `bool ShouldSave(float now)`, `void Saved()`

- [ ] **Step 1: Repo scaffolding and test project**

`.gitignore`:

```
node_modules
.env
.env.*
.DS_Store
dist
build
*.log
.superpowers
.sai/

# .NET
bin/
obj/
*.user
Local.props
```

`tests/ModSettings.Tests/ModSettings.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <RollForward>Major</RollForward>
  </PropertyGroup>

  <!-- Only the game-independent logic is tested; it's compiled in directly so no game assemblies are needed. -->
  <ItemGroup>
    <Compile Include="../../src/ModSettings/KeyNames.cs" Link="KeyNames.cs" />
    <Compile Include="../../src/ModSettings/Names.cs" Link="Names.cs" />
    <Compile Include="../../src/ModSettings/EditorKinds.cs" Link="EditorKinds.cs" />
    <Compile Include="../../src/ModSettings/ValueParsing.cs" Link="ValueParsing.cs" />
    <Compile Include="../../src/ModSettings/SettingOrder.cs" Link="SettingOrder.cs" />
    <Compile Include="../../src/ModSettings/Picker.cs" Link="Picker.cs" />
    <Compile Include="../../src/ModSettings/HotkeyGate.cs" Link="HotkeyGate.cs" />
    <Compile Include="../../src/ModSettings/SaveSchedule.cs" Link="SaveSchedule.cs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the failing tests**

`tests/ModSettings.Tests/KeyNamesTests.cs`:

```csharp
using ModSettings;
using Xunit;

public class KeyNamesTests
{
    [Theory]
    [InlineData("Return", "Enter")]
    [InlineData("KeypadEnter", "NumpadEnter")]
    [InlineData("Alpha1", "Digit1")]
    [InlineData("Keypad5", "Numpad5")]
    [InlineData("LeftControl", "LeftCtrl")]
    [InlineData("LeftCommand", "LeftMeta")]
    [InlineData("BackQuote", "Backquote")]
    [InlineData("Print", "PrintScreen")]
    [InlineData("Menu", "ContextMenu")]
    [InlineData("F10", "F10")]
    [InlineData("A", "A")]
    public void MapsKeyCodeNamesToInputSystemNames(string keyCode, string key)
    {
        Assert.Equal(key, KeyNames.ToKeyName(keyCode));
    }

    [Theory]
    [InlineData("Return")]
    [InlineData("KeypadEnter")]
    [InlineData("Alpha0")]
    [InlineData("Keypad9")]
    [InlineData("RightControl")]
    [InlineData("RightCommand")]
    [InlineData("Print")]
    [InlineData("F1")]
    [InlineData("Space")]
    public void RoundTrips(string keyCode)
    {
        Assert.Equal(keyCode, KeyNames.ToKeyCodeName(KeyNames.ToKeyName(keyCode)));
    }

    [Fact]
    public void KnowsModifiers()
    {
        Assert.True(KeyNames.IsModifier("LeftShift"));
        Assert.True(KeyNames.IsModifier("RightControl"));
        Assert.True(KeyNames.IsModifier("LeftAlt"));
        Assert.True(KeyNames.IsModifier("LeftCommand"));
        Assert.False(KeyNames.IsModifier("F10"));
        Assert.False(KeyNames.IsModifier("Space"));
    }
}
```

`tests/ModSettings.Tests/NamesTests.cs`:

```csharp
using ModSettings;
using Xunit;

public class NamesTests
{
    [Theory]
    [InlineData("MaxPlayers", "Max Players")]
    [InlineData("CounterOffsetX", "Counter Offset X")]
    [InlineData("HTTPServer", "HTTP Server")]
    [InlineData("snake_case", "snake case")]
    [InlineData("Already spaced", "Already spaced")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void Humanizes(string key, string expected)
    {
        Assert.Equal(expected, Names.Humanize(key));
    }
}
```

`tests/ModSettings.Tests/EditorKindsTests.cs`:

```csharp
using System;
using ModSettings;
using Xunit;

public class EditorKindsTests
{
    private enum Two { A, B }

    [Flags]
    private enum Flagged { None = 0, A = 1, B = 2 }

    [Fact]
    public void BoolIsAToggle() => Assert.Equal(EditorKind.Toggle, EditorKinds.Choose(typeof(bool), false, Acceptable.None, 0));

    [Fact]
    public void ShortcutIsAKey() => Assert.Equal(EditorKind.Key, EditorKinds.Choose(typeof(object), true, Acceptable.None, 0));

    [Theory]
    [InlineData(2, EditorKind.Cycle)]
    [InlineData(6, EditorKind.Cycle)]
    [InlineData(7, EditorKind.Dropdown)]
    public void ListsCycleUpToSixThenDrop(int count, EditorKind expected)
    {
        Assert.Equal(expected, EditorKinds.Choose(typeof(string), false, Acceptable.List, count));
        Assert.Equal(expected, EditorKinds.Choose(typeof(int), false, Acceptable.List, count));
    }

    [Fact]
    public void SmallEnumCycles() => Assert.Equal(EditorKind.Cycle, EditorKinds.Choose(typeof(Two), false, Acceptable.None, 0));

    [Fact]
    public void LargeEnumDropsDown() => Assert.Equal(EditorKind.Dropdown, EditorKinds.Choose(typeof(DayOfWeek), false, Acceptable.None, 0));

    [Fact]
    public void FlagsEnumIsReadOnly() => Assert.Equal(EditorKind.ReadOnly, EditorKinds.Choose(typeof(Flagged), false, Acceptable.None, 0));

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(byte))]
    [InlineData(typeof(ushort))]
    public void RangedIntegersSlideInSteps(Type type) => Assert.Equal(EditorKind.SliderInt, EditorKinds.Choose(type, false, Acceptable.Range, 0));

    [Theory]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    public void RangedRealsSlide(Type type) => Assert.Equal(EditorKind.Slider, EditorKinds.Choose(type, false, Acceptable.Range, 0));

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(ulong))]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    public void UnrangedNumbersAreTyped(Type type) => Assert.Equal(EditorKind.NumberText, EditorKinds.Choose(type, false, Acceptable.None, 0));

    [Fact]
    public void StringIsText() => Assert.Equal(EditorKind.Text, EditorKinds.Choose(typeof(string), false, Acceptable.None, 0));

    [Fact]
    public void RangedStringIsStillText() => Assert.Equal(EditorKind.Text, EditorKinds.Choose(typeof(string), false, Acceptable.Range, 0));

    [Fact]
    public void AnythingElseIsReadOnly() => Assert.Equal(EditorKind.ReadOnly, EditorKinds.Choose(typeof(DateTime), false, Acceptable.None, 0));
}
```

`tests/ModSettings.Tests/ValueParsingTests.cs`:

```csharp
using System;
using System.Globalization;
using System.Threading;
using ModSettings;
using Xunit;

public class ValueParsingTests
{
    [Theory]
    [InlineData("42", typeof(int), 42)]
    [InlineData(" 7 ", typeof(int), 7)]
    [InlineData("-5", typeof(sbyte), (sbyte)-5)]
    [InlineData("255", typeof(byte), (byte)255)]
    [InlineData("18446744073709551615", typeof(ulong), ulong.MaxValue)]
    [InlineData("1.5", typeof(float), 1.5f)]
    [InlineData("1e3", typeof(double), 1000d)]
    public void ParsesNumbers(string text, Type type, object expected)
    {
        Assert.True(ValueParsing.TryParseNumber(text, type, out var value));
        Assert.Equal(expected, value);
        Assert.IsType(type, value);
    }

    [Fact]
    public void ParsesDecimal()
    {
        Assert.True(ValueParsing.TryParseNumber("2.25", typeof(decimal), out var value));
        Assert.Equal(2.25m, value);
    }

    [Theory]
    [InlineData("3.5", typeof(int))]
    [InlineData("99999999999", typeof(int))]
    [InlineData("300", typeof(byte))]
    [InlineData("-1", typeof(uint))]
    [InlineData("1,5", typeof(float))]
    [InlineData("NaN", typeof(float))]
    [InlineData("Infinity", typeof(double))]
    [InlineData("abc", typeof(double))]
    [InlineData("", typeof(int))]
    [InlineData(null, typeof(int))]
    [InlineData("5", typeof(string))]
    public void RejectsInvalid(string text, Type type)
    {
        Assert.False(ValueParsing.TryParseNumber(text, type, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void ParsesWithInvariantCulture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Assert.True(ValueParsing.TryParseNumber("1.5", typeof(float), out var value));
            Assert.Equal(1.5f, value);
            Assert.Equal("1.5", ValueParsing.Format(1.5f));
            Assert.Equal("1.5", ValueParsing.FormatSlider(1.5f, integer: false));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ChecksRanges()
    {
        Assert.True(ValueParsing.InRange(5, 1, 10));
        Assert.True(ValueParsing.InRange(1, 1, 10));
        Assert.True(ValueParsing.InRange(10, 1, 10));
        Assert.False(ValueParsing.InRange(0, 1, 10));
        Assert.False(ValueParsing.InRange(0.5f, 1f, 2f));
    }

    [Theory]
    [InlineData(2.4, 2)]
    [InlineData(2.5, 3)]
    [InlineData(-2.5, -3)]
    public void SnapsIntegers(double raw, double expected) => Assert.Equal(expected, ValueParsing.SnapInteger(raw));

    [Theory]
    [InlineData(0.123456f, 0f, 1f, 0.12f)]
    [InlineData(57.3f, 0f, 250f, 57f)]
    [InlineData(5f, 0f, 1f, 1f)]
    [InlineData(-3f, 0f, 1f, 0f)]
    public void RoundsSlidersToARangeSizedStep(float raw, float low, float high, float expected)
    {
        Assert.Equal(expected, ValueParsing.RoundToStep(raw, low, high), 4);
    }

    [Fact]
    public void FormatsSliderLabels()
    {
        Assert.Equal("12", ValueParsing.FormatSlider(12f, integer: true));
        Assert.Equal("0.25", ValueParsing.FormatSlider(0.25f, integer: false));
        Assert.Equal("2", ValueParsing.FormatSlider(2f, integer: false));
    }
}
```

`tests/ModSettings.Tests/SettingOrderTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using ModSettings;
using Xunit;

public class SettingOrderTests
{
    private static List<SettingGroup<SettingFacts>> Group(params SettingFacts[] items) => SettingOrder.Group(items, f => f);

    private static SettingFacts S(string section, string name, int order = 0, bool advanced = false, bool hidden = false) =>
        new SettingFacts { Section = section, Name = name, Order = order, Advanced = advanced, Hidden = hidden };

    [Fact]
    public void SectionsKeepConfigOrder()
    {
        var groups = Group(S("Zeta", "a"), S("Alpha", "b"), S("Zeta", "c"));
        Assert.Equal(new[] { "Zeta", "Alpha" }, groups.Select(g => g.Heading));
    }

    [Fact]
    public void HigherOrderFirstThenNameIgnoringCase()
    {
        var groups = Group(S("G", "beta"), S("G", "Alpha"), S("G", "low", order: -1), S("G", "top", order: 5));
        Assert.Equal(new[] { "top", "Alpha", "beta", "low" }, groups[0].Items.Select(i => i.Name));
    }

    [Fact]
    public void AdvancedGoesLastUnderItsOwnHeading()
    {
        var groups = Group(S("A", "adv", advanced: true), S("A", "plain"), S("B", "other"));
        Assert.Equal(new[] { "A", "B", "Advanced" }, groups.Select(g => g.Heading));
        Assert.True(groups[2].IsAdvanced);
        Assert.Equal("adv", groups[2].Items.Single().Name);
    }

    [Fact]
    public void HiddenAreDropped()
    {
        var groups = Group(S("A", "shown"), S("A", "gone", hidden: true), S("B", "gone too", hidden: true));
        Assert.Equal("A", groups.Single().Heading);
        Assert.Equal("shown", groups[0].Items.Single().Name);
    }

    [Fact]
    public void OnlyAdvancedGivesOneGroup()
    {
        var groups = Group(S("A", "x", advanced: true));
        Assert.Equal("Advanced", groups.Single().Heading);
    }

    [Fact]
    public void EmptyGivesNoGroups() => Assert.Empty(Group());

    [Fact]
    public void ModsWithOnlyHiddenSettingsAreEmpty()
    {
        Assert.False(SettingOrder.AnyVisible(new[] { S("A", "x", hidden: true) }, f => f));
        Assert.True(SettingOrder.AnyVisible(new[] { S("A", "x", hidden: true), S("A", "y") }, f => f));
        Assert.False(SettingOrder.AnyVisible(new SettingFacts[0], f => f));
    }
}
```

`tests/ModSettings.Tests/PickerTests.cs`:

```csharp
using ModSettings;
using Xunit;

public class PickerTests
{
    [Theory]
    [InlineData(0, 3, 0)]
    [InlineData(3, 3, 0)]
    [InlineData(-1, 3, 2)]
    [InlineData(5, 1, 0)]
    [InlineData(0, 0, -1)]
    public void Wraps(int index, int count, int expected) => Assert.Equal(expected, Picker.Wrap(index, count));

    [Fact]
    public void FindsTheRememberedMod() => Assert.Equal(1, Picker.IndexOf(new[] { "a", "b" }, "b"));

    [Fact]
    public void FallsBackToTheFirstWhenTheRememberedModIsGone() => Assert.Equal(0, Picker.IndexOf(new[] { "a", "b" }, "gone"));

    [Fact]
    public void FallsBackToTheFirstWhenNothingIsRemembered() => Assert.Equal(0, Picker.IndexOf(new[] { "a" }, null));

    [Fact]
    public void NoModsGivesMinusOne() => Assert.Equal(-1, Picker.IndexOf(new string[0], "a"));
}
```

`tests/ModSettings.Tests/HotkeyGateTests.cs`:

```csharp
using ModSettings;
using Xunit;

public class HotkeyGateTests
{
    private static HotkeyState Ready() => new HotkeyState { HasMenuHost = true };

    [Fact]
    public void OpensOptionsFromGameplayOrTheTitle() => Assert.Equal(HotkeyAction.OpenOptions, HotkeyGate.Decide(Ready()));

    [Fact]
    public void SwitchesTabWhenOptionsIsOpen()
    {
        var s = Ready();
        s.OptionsActive = true;
        Assert.Equal(HotkeyAction.ShowModsTab, HotkeyGate.Decide(s));
    }

    [Fact]
    public void IgnoredWhileTyping()
    {
        var s = Ready();
        s.OptionsActive = true;
        s.TextFieldFocused = true;
        Assert.Equal(HotkeyAction.None, HotkeyGate.Decide(s));
    }

    [Fact]
    public void IgnoredWhileRebinding()
    {
        var s = Ready();
        s.OptionsActive = true;
        s.Rebinding = true;
        Assert.Equal(HotkeyAction.None, HotkeyGate.Decide(s));
    }

    [Fact]
    public void IgnoredWhileLoading()
    {
        var s = Ready();
        s.Loading = true;
        Assert.Equal(HotkeyAction.None, HotkeyGate.Decide(s));
    }

    [Fact]
    public void IgnoredWhileMenusAreSuppressed()
    {
        var s = Ready();
        s.MenusSuppressed = true;
        Assert.Equal(HotkeyAction.None, HotkeyGate.Decide(s));
    }

    [Fact]
    public void IgnoredWithoutAMenuToOpenFrom()
    {
        var s = Ready();
        s.HasMenuHost = false;
        Assert.Equal(HotkeyAction.None, HotkeyGate.Decide(s));
    }

    [Fact]
    public void IgnoredWhenAnotherMenuIsOpen()
    {
        var s = Ready();
        s.OtherMenuOpen = true;
        Assert.Equal(HotkeyAction.None, HotkeyGate.Decide(s));
    }

    [Fact]
    public void IgnoredWhenAModalIsOverOptions()
    {
        var s = Ready();
        s.OptionsActive = true;
        s.OtherMenuOpen = true;
        Assert.Equal(HotkeyAction.None, HotkeyGate.Decide(s));
    }
}
```

`tests/ModSettings.Tests/SaveScheduleTests.cs`:

```csharp
using ModSettings;
using Xunit;

public class SaveScheduleTests
{
    [Fact]
    public void NothingToSaveWhenClean()
    {
        var s = new SaveSchedule();
        Assert.False(s.Dirty);
        Assert.False(s.ShouldSave(100f));
    }

    [Fact]
    public void SavesASecondAfterTheFirstChange()
    {
        var s = new SaveSchedule();
        s.MarkDirty(10f);
        Assert.True(s.Dirty);
        Assert.False(s.ShouldSave(10.5f));
        Assert.True(s.ShouldSave(11f));
    }

    [Fact]
    public void LaterChangesDoNotPostponeTheSave()
    {
        var s = new SaveSchedule();
        s.MarkDirty(0f);
        s.MarkDirty(0.9f);
        Assert.True(s.ShouldSave(1f));
    }

    [Fact]
    public void SavedClearsAndTheNextChangeStartsANewWindow()
    {
        var s = new SaveSchedule();
        s.MarkDirty(0f);
        s.Saved();
        Assert.False(s.Dirty);
        Assert.False(s.ShouldSave(5f));
        s.MarkDirty(5f);
        Assert.False(s.ShouldSave(5.5f));
        Assert.True(s.ShouldSave(6f));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/ModSettings.Tests/ModSettings.Tests.csproj 2>&1 | tail -5`
Expected: the build FAILS with CS2001, "Source file '…/src/ModSettings/KeyNames.cs' could not be found" (and similar for the other files).

- [ ] **Step 4: Write the implementations**

`src/ModSettings/KeyNames.cs`:

```csharp
using System;

namespace ModSettings
{
    /// <summary>
    /// Converts between Unity <c>KeyCode</c> names (what BepInEx's KeyboardShortcut stores) and Input System
    /// <c>Key</c> names (what the game's keyboard reports). Most names match; these are the ones that don't.
    /// </summary>
    internal static class KeyNames
    {
        private static readonly (string KeyCode, string Key)[] Renamed =
        {
            ("Return", "Enter"),
            ("KeypadEnter", "NumpadEnter"),
            ("KeypadPlus", "NumpadPlus"),
            ("KeypadMinus", "NumpadMinus"),
            ("KeypadMultiply", "NumpadMultiply"),
            ("KeypadDivide", "NumpadDivide"),
            ("KeypadPeriod", "NumpadPeriod"),
            ("KeypadEquals", "NumpadEquals"),
            ("LeftControl", "LeftCtrl"),
            ("RightControl", "RightCtrl"),
            ("LeftCommand", "LeftMeta"),
            ("RightCommand", "RightMeta"),
            ("BackQuote", "Backquote"),
            ("Print", "PrintScreen"),
            ("Numlock", "NumLock"),
            ("Menu", "ContextMenu"),
        };

        private static readonly string[] Modifiers =
        {
            "LeftControl", "RightControl", "LeftShift", "RightShift", "LeftAlt", "RightAlt", "LeftCommand", "RightCommand", "AltGr",
        };

        public static string ToKeyName(string keyCode)
        {
            foreach (var (code, key) in Renamed)
            {
                if (code == keyCode)
                    return key;
            }
            if (keyCode.StartsWith("Alpha", StringComparison.Ordinal))
                return "Digit" + keyCode.Substring("Alpha".Length);
            if (keyCode.StartsWith("Keypad", StringComparison.Ordinal))
                return "Numpad" + keyCode.Substring("Keypad".Length);
            return keyCode;
        }

        public static string ToKeyCodeName(string key)
        {
            foreach (var (code, name) in Renamed)
            {
                if (name == key)
                    return code;
            }
            if (key.StartsWith("Digit", StringComparison.Ordinal))
                return "Alpha" + key.Substring("Digit".Length);
            if (key.StartsWith("Numpad", StringComparison.Ordinal))
                return "Keypad" + key.Substring("Numpad".Length);
            return key;
        }

        public static bool IsModifier(string keyCode) => Array.IndexOf(Modifiers, keyCode) >= 0;
    }
}
```

`src/ModSettings/Names.cs`:

```csharp
using System.Collections.Generic;

namespace ModSettings
{
    internal static class Names
    {
        /// <summary>"CounterOffsetX" becomes "Counter Offset X"; keys that already have spaces are left alone.</summary>
        public static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Contains(" "))
                return key;

            var chars = new List<char>(key.Length + 8);
            for (var i = 0; i < key.Length; i++)
            {
                var c = key[i];
                if (c == '_')
                {
                    chars.Add(' ');
                    continue;
                }
                if (i > 0 && char.IsUpper(c) && key[i - 1] != '_'
                    && (char.IsLower(key[i - 1]) || (i + 1 < key.Length && char.IsLower(key[i + 1]) && char.IsUpper(key[i - 1]))))
                    chars.Add(' ');
                chars.Add(c);
            }
            return new string(chars.ToArray());
        }
    }
}
```

`src/ModSettings/EditorKinds.cs`:

```csharp
using System;

namespace ModSettings
{
    internal enum EditorKind
    {
        Toggle,
        Cycle,
        Dropdown,
        SliderInt,
        Slider,
        NumberText,
        Text,
        Key,
        ReadOnly,
    }

    /// <summary>The kind of AcceptableValueBase a setting declares.</summary>
    internal enum Acceptable
    {
        None,
        List,
        Range,
    }

    /// <summary>Picks the control for a setting from its type and acceptable values (the spec's control table).</summary>
    internal static class EditorKinds
    {
        public const int MaxCycleChoices = 6;

        private static readonly Type[] Integers =
            { typeof(int), typeof(long), typeof(short), typeof(byte), typeof(sbyte), typeof(uint), typeof(ushort), typeof(ulong) };

        private static readonly Type[] Reals = { typeof(float), typeof(double), typeof(decimal) };

        public static bool IsInteger(Type type) => Array.IndexOf(Integers, type) >= 0;

        public static bool IsReal(Type type) => Array.IndexOf(Reals, type) >= 0;

        /// <param name="isShortcut">The type is BepInEx's KeyboardShortcut (not referenced here so this stays testable).</param>
        /// <param name="listCount">Number of values when <paramref name="acceptable"/> is List.</param>
        public static EditorKind Choose(Type type, bool isShortcut, Acceptable acceptable, int listCount)
        {
            if (type == typeof(bool))
                return EditorKind.Toggle;
            if (isShortcut)
                return EditorKind.Key;
            if (acceptable == Acceptable.List)
                return listCount > MaxCycleChoices ? EditorKind.Dropdown : EditorKind.Cycle;
            if (type.IsEnum)
            {
                if (type.IsDefined(typeof(FlagsAttribute), false))
                    return EditorKind.ReadOnly;
                return Enum.GetValues(type).Length > MaxCycleChoices ? EditorKind.Dropdown : EditorKind.Cycle;
            }
            if (acceptable == Acceptable.Range)
            {
                if (IsInteger(type))
                    return EditorKind.SliderInt;
                if (IsReal(type))
                    return EditorKind.Slider;
            }
            if (IsInteger(type) || IsReal(type))
                return EditorKind.NumberText;
            if (type == typeof(string))
                return EditorKind.Text;
            return EditorKind.ReadOnly;
        }
    }
}
```

`src/ModSettings/ValueParsing.cs`:

```csharp
using System;
using System.Globalization;

namespace ModSettings
{
    /// <summary>Number parsing and formatting for typed and slider settings. Always the invariant culture, like BepInEx's config files.</summary>
    internal static class ValueParsing
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static bool TryParseNumber(string text, Type type, out object value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            text = text.Trim();

            const NumberStyles integer = NumberStyles.Integer;
            const NumberStyles real = NumberStyles.Float;
            if (type == typeof(int)) return Wrap(int.TryParse(text, integer, Inv, out var i), i, out value);
            if (type == typeof(long)) return Wrap(long.TryParse(text, integer, Inv, out var l), l, out value);
            if (type == typeof(short)) return Wrap(short.TryParse(text, integer, Inv, out var s), s, out value);
            if (type == typeof(byte)) return Wrap(byte.TryParse(text, integer, Inv, out var b), b, out value);
            if (type == typeof(sbyte)) return Wrap(sbyte.TryParse(text, integer, Inv, out var sb), sb, out value);
            if (type == typeof(uint)) return Wrap(uint.TryParse(text, integer, Inv, out var ui), ui, out value);
            if (type == typeof(ushort)) return Wrap(ushort.TryParse(text, integer, Inv, out var us), us, out value);
            if (type == typeof(ulong)) return Wrap(ulong.TryParse(text, integer, Inv, out var ul), ul, out value);
            if (type == typeof(float))
                return Wrap(float.TryParse(text, real, Inv, out var f) && !float.IsNaN(f) && !float.IsInfinity(f), f, out value);
            if (type == typeof(double))
                return Wrap(double.TryParse(text, real, Inv, out var d) && !double.IsNaN(d) && !double.IsInfinity(d), d, out value);
            if (type == typeof(decimal))
                return Wrap(decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowExponent, Inv, out var m), m, out value);
            return false;
        }

        private static bool Wrap<T>(bool ok, T parsed, out object value)
        {
            value = ok ? (object)parsed : null;
            return ok;
        }

        /// <summary>Inclusive range check; the three values share the setting's type.</summary>
        public static bool InRange(object value, object min, object max)
        {
            var comparable = (IComparable)value;
            return comparable.CompareTo(min) >= 0 && comparable.CompareTo(max) <= 0;
        }

        public static double SnapInteger(double value) => Math.Round(value, MidpointRounding.AwayFromZero);

        /// <summary>Rounds to a step that suits the range (1/100 of its order of magnitude) so files don't fill with float noise.</summary>
        public static float RoundToStep(float value, float low, float high)
        {
            var step = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(high - low, 1e-6))) - 2);
            var rounded = Math.Round(value / step) * step;
            return (float)Math.Min(high, Math.Max(low, rounded));
        }

        public static string Format(object value) =>
            value is IFormattable formattable ? formattable.ToString(null, Inv) : value?.ToString() ?? "";

        public static string FormatSlider(float value, bool integer) => value.ToString(integer ? "0" : "0.##", Inv);
    }
}
```

`src/ModSettings/SettingOrder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModSettings
{
    /// <summary>What ordering and grouping need to know about one setting.</summary>
    internal struct SettingFacts
    {
        public string Section;
        public string Name;
        public int Order;
        public bool Advanced;
        public bool Hidden;
    }

    internal sealed class SettingGroup<T>
    {
        public string Heading;
        public bool IsAdvanced;
        public List<T> Items = new List<T>();
    }

    internal static class SettingOrder
    {
        public const string AdvancedHeading = "Advanced";

        /// <summary>
        /// Sections in the order they first appear (config order); inside each, higher Order first, then name.
        /// Advanced settings from every section are gathered into one last group. Hidden ones are dropped.
        /// </summary>
        public static List<SettingGroup<T>> Group<T>(IEnumerable<T> items, Func<T, SettingFacts> facts)
        {
            var groups = new List<SettingGroup<T>>();
            var bySection = new Dictionary<string, SettingGroup<T>>(StringComparer.Ordinal);
            var advanced = new SettingGroup<T> { Heading = AdvancedHeading, IsAdvanced = true };

            foreach (var item in items)
            {
                var f = facts(item);
                if (f.Hidden)
                    continue;
                if (f.Advanced)
                {
                    advanced.Items.Add(item);
                    continue;
                }
                var section = f.Section ?? "";
                if (!bySection.TryGetValue(section, out var group))
                {
                    group = new SettingGroup<T> { Heading = section };
                    bySection.Add(section, group);
                    groups.Add(group);
                }
                group.Items.Add(item);
            }

            if (advanced.Items.Count > 0)
                groups.Add(advanced);
            foreach (var group in groups)
            {
                group.Items = group.Items
                    .OrderByDescending(i => facts(i).Order)
                    .ThenBy(i => facts(i).Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            return groups;
        }

        public static bool AnyVisible<T>(IEnumerable<T> items, Func<T, SettingFacts> facts) => items.Any(i => !facts(i).Hidden);
    }
}
```

`src/ModSettings/Picker.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace ModSettings
{
    /// <summary>The Mods tab's mod selector: wrap-around stepping and restoring the session's last pick.</summary>
    internal static class Picker
    {
        public static int Wrap(int index, int count) => count <= 0 ? -1 : ((index % count) + count) % count;

        public static int IndexOf(IReadOnlyList<string> ids, string remembered)
        {
            if (ids.Count == 0)
                return -1;
            for (var i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], remembered, StringComparison.Ordinal))
                    return i;
            }
            return 0;
        }
    }
}
```

`src/ModSettings/HotkeyGate.cs`:

```csharp
namespace ModSettings
{
    internal enum HotkeyAction
    {
        None,
        ShowModsTab,
        OpenOptions,
    }

    internal struct HotkeyState
    {
        /// <summary>An Options screen is enabled.</summary>
        public bool OptionsActive;
        public bool TextFieldFocused;
        public bool Rebinding;
        public bool Loading;
        /// <summary>MenuSceneManager.MenusSuppressed: the developer console or the Admin Menu owns input.</summary>
        public bool MenusSuppressed;
        /// <summary>A pause menu (in a run) or title screen exists to open Options from.</summary>
        public bool HasMenuHost;
        /// <summary>A menu other than the pause menu, title screen or Options is open (results screen, modal, …).</summary>
        public bool OtherMenuOpen;
    }

    internal static class HotkeyGate
    {
        public static HotkeyAction Decide(HotkeyState s)
        {
            if (s.TextFieldFocused || s.Rebinding || s.Loading || s.MenusSuppressed || s.OtherMenuOpen)
                return HotkeyAction.None;
            if (s.OptionsActive)
                return HotkeyAction.ShowModsTab;
            return s.HasMenuHost ? HotkeyAction.OpenOptions : HotkeyAction.None;
        }
    }
}
```

`src/ModSettings/SaveSchedule.cs`:

```csharp
namespace ModSettings
{
    /// <summary>When to save changed config files: once the first unsaved change is a second old.</summary>
    internal sealed class SaveSchedule
    {
        private readonly float _interval;
        private float _due;

        public SaveSchedule(float interval = 1f)
        {
            _interval = interval;
        }

        public bool Dirty { get; private set; }

        public void MarkDirty(float now)
        {
            if (Dirty)
                return;
            Dirty = true;
            _due = now + _interval;
        }

        public bool ShouldSave(float now) => Dirty && now >= _due;

        public void Saved() => Dirty = false;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/ModSettings.Tests/ModSettings.Tests.csproj 2>&1 | tail -3`
Expected: `Passed!  - Failed:     0, Passed:    83` (the count is approximate; there must be no failures).

- [ ] **Step 6: Commit**

```bash
git add .gitignore src tests
git commit -m "feat: pure logic for control choice, parsing, ordering, picker, hotkey gate and saves

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Plugin project, config and hotkey reading

**Files:**
- Create:
  - `src/ModSettings/ModSettings.csproj`
  - `src/ModSettings/Plugin.cs`
  - `src/ModSettings/ConfigurationManagerAttributes.cs`
  - `src/ModSettings/Hotkey.cs`

**Interfaces:**
- Consumes: `KeyNames` (Task 1).
- Produces:
  - `Plugin.Log` (ManualLogSource), `Plugin.OpenKey` (`ConfigEntry<KeyboardShortcut>`), `Plugin.PluginGuid`/`PluginName`/`PluginVersion`
  - `Hotkey.WasPressed(KeyboardShortcut) → bool`, `Hotkey.TryGetKey(KeyCode, out Key) → bool`, `Hotkey.TryGetKeyCode(Key, out KeyCode) → bool`

- [ ] **Step 1: Project file**

`src/ModSettings/ModSettings.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <AssemblyName>ModSettings</AssemblyName>
    <RootNamespace>ModSettings</RootNamespace>
    <Version>0.1.0</Version>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <DebugType>embedded</DebugType>
  </PropertyGroup>

  <!-- Machine-specific paths. Override in Local.props (gitignored) or with -p:GamePath=... -->
  <Import Project="$(MSBuildThisFileDirectory)../../Local.props" Condition="Exists('$(MSBuildThisFileDirectory)../../Local.props')" />

  <!-- There's no r2modman community for Trash Day yet, so BepInEx is installed straight into the game folder. -->
  <PropertyGroup>
    <GamePath Condition="'$(GamePath)' == ''">/var/mnt/data/SteamLibrary/steamapps/common/TrashDay</GamePath>
    <ProfilePath Condition="'$(ProfilePath)' == ''">$(GamePath)</ProfilePath>
    <ManagedPath>$(GamePath)/Trash_Data/Managed</ManagedPath>
    <BepInExCorePath>$(ProfilePath)/BepInEx/core</BepInExCorePath>
    <DeployPath>$(ProfilePath)/BepInEx/plugins/darkharasho-ModSettings</DeployPath>
  </PropertyGroup>

  <!-- Without the game installed (CI), or with -p:UseRefs=true, build against the reference
       assemblies from scripts/update-refs.sh. -->
  <PropertyGroup Condition="'$(UseRefs)' == 'true' Or !Exists('$(ManagedPath)/Assembly-CSharp.dll')">
    <ManagedPath>$(MSBuildThisFileDirectory)../../lib/refs</ManagedPath>
  </PropertyGroup>
  <PropertyGroup Condition="'$(UseRefs)' == 'true' Or !Exists('$(BepInExCorePath)/BepInEx.dll')">
    <BepInExCorePath>$(MSBuildThisFileDirectory)../../lib/refs</BepInExCorePath>
  </PropertyGroup>

  <!-- Keep in sync with game_dlls/bepinex_dlls in scripts/update-refs.sh. -->
  <ItemGroup>
    <Reference Include="BepInEx" HintPath="$(BepInExCorePath)/BepInEx.dll" Private="false" />
    <Reference Include="0Harmony" HintPath="$(BepInExCorePath)/0Harmony.dll" Private="false" />
    <Reference Include="Assembly-CSharp" HintPath="$(ManagedPath)/Assembly-CSharp.dll" Private="false" />
    <Reference Include="UnityEngine" HintPath="$(ManagedPath)/UnityEngine.dll" Private="false" />
    <Reference Include="UnityEngine.CoreModule" HintPath="$(ManagedPath)/UnityEngine.CoreModule.dll" Private="false" />
    <Reference Include="UnityEngine.TextRenderingModule" HintPath="$(ManagedPath)/UnityEngine.TextRenderingModule.dll" Private="false" />
    <Reference Include="UnityEngine.UIElementsModule" HintPath="$(ManagedPath)/UnityEngine.UIElementsModule.dll" Private="false" />
    <Reference Include="Unity.InputSystem" HintPath="$(ManagedPath)/Unity.InputSystem.dll" Private="false" />
  </ItemGroup>

  <Target Name="DeployToProfile" AfterTargets="Build" Condition="Exists('$(ProfilePath)/BepInEx/plugins')">
    <MakeDir Directories="$(DeployPath)" />
    <Copy SourceFiles="$(TargetPath)" DestinationFolder="$(DeployPath)" />
    <Message Importance="high" Text="Deployed $(TargetFileName) to $(DeployPath)" />
  </Target>

</Project>
```

The project lives beside the test project, and the test project only links the pure files. The test project must not be picked up by this csproj's default glob; it isn't, because it sits under `tests/`.

- [ ] **Step 2: Plugin, attributes and hotkey reader**

`src/ModSettings/ConfigurationManagerAttributes.cs`:

```csharp
namespace ModSettings
{
    /// <summary>
    /// Tag for a setting's <see cref="BepInEx.Configuration.ConfigDescription"/>, read by field name by settings
    /// menus (this mod and BepInEx.ConfigurationManager).
    /// </summary>
    internal sealed class ConfigurationManagerAttributes
    {
        public string DispName;
        public int? Order;
    }
}
```

`src/ModSettings/Hotkey.cs`:

```csharp
using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ModSettings
{
    /// <summary>
    /// Reads a <see cref="KeyboardShortcut"/> straight from the keyboard through the Input System, so it works
    /// whatever the game's own input actions are doing.
    /// </summary>
    internal static class Hotkey
    {
        public static bool WasPressed(KeyboardShortcut shortcut)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || shortcut.MainKey == KeyCode.None)
                return false;
            if (!TryGetKey(shortcut.MainKey, out var main) || !keyboard[main].wasPressedThisFrame)
                return false;
            foreach (var modifier in shortcut.Modifiers)
            {
                if (!TryGetKey(modifier, out var key) || !keyboard[key].isPressed)
                    return false;
            }
            return true;
        }

        public static bool TryGetKey(KeyCode code, out Key key) =>
            Enum.TryParse(KeyNames.ToKeyName(code.ToString()), true, out key) && key != Key.None;

        public static bool TryGetKeyCode(Key key, out KeyCode code) =>
            Enum.TryParse(KeyNames.ToKeyCodeName(key.ToString()), true, out code) && code != KeyCode.None;
    }
}
```

`src/ModSettings/Plugin.cs`:

```csharp
using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ModSettings
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.darkharasho.trashday.modsettings";
        public const string PluginName = "ModSettings";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> OpenKey;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            OpenKey = Config.Bind("General", "OpenKey", new KeyboardShortcut(KeyCode.F10), new ConfigDescription(
                "Opens the game's Options screen on the Mods tab.",
                null,
                new ConfigurationManagerAttributes { DispName = "Open key" }));

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll();
            }
            catch (Exception e)
            {
                Log.LogError($"Patching failed, likely a game update; the Mods tab is unavailable. {e}");
                _harmony.UnpatchSelf();
                return;
            }

            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();
    }
}
```

- [ ] **Step 3: Build against the game**

Run: `dotnet build src/ModSettings/ModSettings.csproj 2>&1 | tail -4`
Expected: `Build succeeded.` with 0 errors, and a `Deployed ModSettings.dll to …/darkharasho-ModSettings` line.

- [ ] **Step 4: Tests still pass**

Run: `dotnet test tests/ModSettings.Tests/ModSettings.Tests.csproj 2>&1 | tail -2`
Expected: `Failed:     0`.

- [ ] **Step 5: Commit**

```bash
git add src/ModSettings/ModSettings.csproj src/ModSettings/Plugin.cs src/ModSettings/ConfigurationManagerAttributes.cs src/ModSettings/Hotkey.cs
git commit -m "feat: plugin project, open-key setting and keyboard reader

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Settings registry and batched writes

**Files:**
- Create:
  - `src/ModSettings/SettingsRegistry.cs`
  - `src/ModSettings/ConfigWriter.cs`

**Interfaces:**
- Consumes: `Names.Humanize`, `SettingFacts`, `SettingOrder.AnyVisible`, `SaveSchedule` (Task 1); `Plugin.Log` (Task 2).
- Produces:
  - `sealed class ModInfo { string Guid; string Name; string Version; List<SettingInfo> Settings; }`
  - `sealed class SettingInfo { ModInfo Mod; ConfigEntryBase Entry; string Section; string Name; string Description; int Order; bool ReadOnly; bool Advanced; bool Hidden; bool RestartRequired; SettingFacts Facts { get; } }`
  - `SettingsRegistry.Collect() → List<ModInfo>`: only mods with a visible setting, sorted by name.
  - `ConfigWriter.Set(ConfigEntryBase entry, object value) → bool`, `ConfigWriter.Tick(float now)`, `ConfigWriter.Flush()`

- [ ] **Step 1: Registry**

`src/ModSettings/SettingsRegistry.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace ModSettings
{
    internal sealed class ModInfo
    {
        public string Guid;
        public string Name;
        public string Version;
        /// <summary>In config order; <see cref="SettingOrder"/> sorts and groups them for display.</summary>
        public List<SettingInfo> Settings = new List<SettingInfo>();
    }

    internal sealed class SettingInfo
    {
        public ModInfo Mod;
        public ConfigEntryBase Entry;
        public string Section;
        public string Name;
        public string Description;
        public int Order;
        public bool ReadOnly;
        public bool Advanced;
        public bool Hidden;
        public bool RestartRequired;

        public SettingFacts Facts => new SettingFacts { Section = Section, Name = Name, Order = Order, Advanced = Advanced, Hidden = Hidden };
    }

    /// <summary>
    /// Finds the settings of every loaded BepInEx plugin: its <see cref="BaseUnityPlugin.Config"/> plus any other
    /// <see cref="ConfigFile"/> fields on the plugin class. A tag object on a setting's <see cref="ConfigDescription"/>
    /// can refine how it's shown, using ConfigurationManagerAttributes' field names (Browsable, Category, DispName,
    /// Order, ReadOnly, IsAdvanced, Description) plus RestartRequired. Tags are read by name, so mods need no
    /// reference to this one.
    /// </summary>
    internal static class SettingsRegistry
    {
        private const BindingFlags AnyField = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly HashSet<string> LoggedFailures = new HashSet<string>();

        public static List<ModInfo> Collect()
        {
            var mods = new List<ModInfo>();
            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                if (plugin?.Instance == null)
                    continue;
                try
                {
                    var mod = new ModInfo
                    {
                        Guid = plugin.Metadata.GUID,
                        Name = plugin.Metadata.Name,
                        Version = plugin.Metadata.Version.ToString(),
                    };
                    foreach (var file in ConfigFiles(plugin.Instance))
                    {
                        foreach (var entry in ((IDictionary<ConfigDefinition, ConfigEntryBase>)file).Values)
                            mod.Settings.Add(Describe(mod, entry));
                    }
                    if (SettingOrder.AnyVisible(mod.Settings, s => s.Facts))
                        mods.Add(mod);
                }
                catch (Exception e)
                {
                    var guid = plugin.Metadata?.GUID ?? "?";
                    if (LoggedFailures.Add(guid))
                        Plugin.Log.LogWarning($"Couldn't read the settings of {guid}; it's left out of the Mods tab. {e}");
                }
            }
            return mods.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IEnumerable<ConfigFile> ConfigFiles(BaseUnityPlugin plugin)
        {
            var seen = new HashSet<ConfigFile> { plugin.Config };
            yield return plugin.Config;

            for (var type = plugin.GetType(); type != null && type != typeof(BaseUnityPlugin); type = type.BaseType)
            {
                foreach (var field in type.GetFields(AnyField | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType != typeof(ConfigFile))
                        continue;
                    var target = field.IsStatic ? null : plugin;
                    if (field.GetValue(target) is ConfigFile file && seen.Add(file))
                        yield return file;
                }
            }
        }

        private static SettingInfo Describe(ModInfo mod, ConfigEntryBase entry)
        {
            var setting = new SettingInfo
            {
                Mod = mod,
                Entry = entry,
                Section = entry.Definition.Section,
                Name = Names.Humanize(entry.Definition.Key),
                Description = entry.Description?.Description,
            };

            var tags = entry.Description?.Tags;
            if (tags == null)
                return setting;

            foreach (var tag in tags)
            {
                if (tag == null)
                    continue;
                setting.Hidden |= Read<bool?>(tag, "Browsable") == false;
                setting.Section = Read<string>(tag, "Category") ?? setting.Section;
                setting.Name = Read<string>(tag, "DispName") ?? setting.Name;
                setting.Description = Read<string>(tag, "Description") ?? setting.Description;
                setting.Order = Read<int?>(tag, "Order") ?? setting.Order;
                setting.ReadOnly |= Read<bool?>(tag, "ReadOnly") == true;
                setting.Advanced |= Read<bool?>(tag, "IsAdvanced") == true;
                setting.RestartRequired |= Read<bool?>(tag, "RestartRequired") == true;
            }
            return setting;
        }

        private static T Read<T>(object tag, string name)
        {
            var type = tag.GetType();
            object value;
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                value = field.GetValue(tag);
            }
            else
            {
                var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (property == null || !property.CanRead)
                    return default;
                value = property.GetValue(tag);
            }
            return value is T typed ? typed : default;
        }
    }
}
```

- [ ] **Step 2: Writer**

`src/ModSettings/ConfigWriter.cs`:

```csharp
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace ModSettings
{
    /// <summary>
    /// Writes settings through <see cref="ConfigEntryBase.BoxedValue"/>, so the owning mod gets its normal
    /// SettingChanged event immediately. Saving is batched (dragging a slider would otherwise rewrite the file on
    /// every step): within a second of the first change, and on <see cref="Flush"/> when Options closes.
    /// </summary>
    internal static class ConfigWriter
    {
        private static readonly HashSet<ConfigFile> Dirty = new HashSet<ConfigFile>();
        private static readonly HashSet<string> LoggedSaveFailures = new HashSet<string>();
        private static readonly SaveSchedule Schedule = new SaveSchedule();

        /// <returns>False if the entry rejected the value (the error is logged).</returns>
        public static bool Set(ConfigEntryBase entry, object value)
        {
            var file = entry.ConfigFile;
            var saveOnSet = file.SaveOnConfigSet;
            try
            {
                file.SaveOnConfigSet = false;
                entry.BoxedValue = value;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't set {entry.Definition} to '{value}': {e}");
                return false;
            }
            finally
            {
                file.SaveOnConfigSet = saveOnSet;
            }
            // Mods that turned SaveOnConfigSet off save on their own terms.
            if (saveOnSet)
            {
                Dirty.Add(file);
                Schedule.MarkDirty(Time.unscaledTime);
            }
            return true;
        }

        public static void Tick(float now)
        {
            if (Schedule.ShouldSave(now))
                Save(now);
        }

        public static void Flush() => Save(Time.unscaledTime);

        private static void Save(float now)
        {
            Schedule.Saved();
            var failed = new List<ConfigFile>();
            foreach (var file in Dirty)
            {
                try
                {
                    file.Save();
                    LoggedSaveFailures.Remove(file.ConfigFilePath);
                }
                catch (Exception e)
                {
                    failed.Add(file);
                    if (LoggedSaveFailures.Add(file.ConfigFilePath))
                        Plugin.Log.LogError($"Couldn't save {file.ConfigFilePath}; retrying. {e}");
                }
            }
            Dirty.Clear();
            foreach (var file in failed)
            {
                Dirty.Add(file);
                Schedule.MarkDirty(now);
            }
        }
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/ModSettings/ModSettings.csproj 2>&1 | tail -4`
Expected: `Build succeeded.`, 0 errors.

- [ ] **Step 4: Tests still pass**

Run: `dotnet test tests/ModSettings.Tests/ModSettings.Tests.csproj 2>&1 | tail -2`
Expected: `Failed:     0`.

- [ ] **Step 5: Commit**

```bash
git add src/ModSettings/SettingsRegistry.cs src/ModSettings/ConfigWriter.cs
git commit -m "feat: collect every plugin's settings and batch config saves

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Rows, editors, key rebinding and the tab model

**Files:**
- Create:
  - `src/ModSettings/Rows.cs`
  - `src/ModSettings/SettingEditors.cs`
  - `src/ModSettings/KeyRebind.cs`
  - `src/ModSettings/ModsTab.cs`

**Interfaces:**
- Consumes:
  - From Task 1: `EditorKinds`, `Acceptable`, `ValueParsing`, `Names`, `Picker`, `SettingOrder`, `KeyNames`
  - From Task 2: `Hotkey.TryGetKeyCode`, `Plugin.Log`
  - From Task 3: `ModInfo`, `SettingInfo`, `SettingsRegistry.Collect`, `ConfigWriter.Set`
- Produces:
  - `Rows.Row(VisualElement parent, string label, string description) → SettingRow`
  - `Rows.Cycle(SettingRow row, Func<string> text, Action next, Action previous, out Action refresh) → Button`
  - `Rows.Slider(SettingRow row, float low, float high, bool integer, Func<float> get, Action<float> set, out Action refresh) → Slider`
  - `Rows.Dropdown(SettingRow row, List<string> labels, Func<int> getIndex, Func<string> currentLabel, Action<int> setIndex, VisualElement skinRoot, out Action refresh) → DropdownField`
  - `Rows.Text(SettingRow row, Func<string> get, Func<string, bool> commit, out Action refresh) → TextField`
  - `Rows.ReadOnly(SettingRow row, Func<string> text, out Action refresh) → Label`
  - `Rows.Heading(VisualElement parent, string text)`, `Rows.Message(VisualElement parent, string text)`, `Rows.Note(SettingRow row, string text) → Label`
  - `SettingEditors.Build(VisualElement parent, SettingInfo setting, VisualElement skinRoot, Action changed) → Action` (returns the row's refresh)
  - `KeyRebind.Listening`, `KeyRebind.ConsumesCancel`, `KeyRebind.Cancel()`, `KeyRebind.Tick()`, `KeyRebind.Create(SettingRow row, ConfigEntryBase entry, Func<object, bool> set, out Action refresh) → Button`
  - `sealed class ModsTab(VisualElement container, VisualElement skinRoot, Action<VisualElement, VisualElement> wireEdges)` with `void Rebuild()` and `void ResetSelectedMod()`

- [ ] **Step 1: Row builders in the game's style**

`src/ModSettings/Rows.cs`:

```csharp
using System;
using System.Collections.Generic;
using Trash;
using UnityEngine;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>
    /// Builds settings rows with the same elements and USS classes as Trash.OptionsUI's AddSlider/AddCycle/
    /// AddDropdown, so the Options screen's own style sheets give them the game's look.
    /// </summary>
    internal static class Rows
    {
        public static SettingRow Row(VisualElement parent, string label, string description)
        {
            var row = new SettingRow();
            var labels = new VisualElement();
            labels.AddToClassList("setting-label");
            labels.style.flexDirection = FlexDirection.Column;

            var name = new Label(label);
            name.AddToClassList("options-text");
            labels.Add(name);

            if (!string.IsNullOrEmpty(description))
            {
                var note = new Label(description);
                note.AddToClassList("options-text");
                note.style.whiteSpace = WhiteSpace.Normal;
                note.style.opacity = 0.7f;
                // Smaller than the name, whatever size the game's sheet gives it.
                name.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    var size = name.resolvedStyle.fontSize * 0.6f;
                    if (size > 0f && Math.Abs(note.resolvedStyle.fontSize - size) > 0.5f)
                        note.style.fontSize = size;
                });
                labels.Add(note);
            }
            row.Add(labels);
            parent.Add(row);
            return row;
        }

        public static Button Cycle(SettingRow row, Func<string> text, Action next, Action previous, out Action refresh)
        {
            var control = new VisualElement();
            control.AddToClassList("cycle-control");
            var button = new Button();
            button.AddToClassList("options-text");
            button.AddToClassList("cycle-btn");
            button.AddToClassList("cycle-btn-single");
            Action update = () => button.text = text();
            button.clicked += () =>
            {
                next();
                update();
            };
            button.RegisterCallback<NavigationMoveEvent>(evt =>
            {
                if (evt.direction != NavigationMoveEvent.Direction.Left && evt.direction != NavigationMoveEvent.Direction.Right)
                    return;
                if (evt.direction == NavigationMoveEvent.Direction.Left)
                    previous();
                else
                    next();
                update();
                evt.StopPropagation();
                button.panel?.focusController?.IgnoreEvent(evt);
            });
            control.Add(button);
            row.Add(control);
            update();
            refresh = update;
            return button;
        }

        public static Slider Slider(SettingRow row, float low, float high, bool integer, Func<float> get, Action<float> set, out Action refresh)
        {
            var control = new VisualElement();
            control.AddToClassList("setting-control");
            var valueLabel = new Label();
            valueLabel.AddToClassList("options-text");
            valueLabel.AddToClassList("slider-value-label");
            var slider = new Slider(low, high);
            slider.AddToClassList("setting-slider");
            slider.RegisterValueChangedCallback(e =>
            {
                var value = integer ? (float)ValueParsing.SnapInteger(e.newValue) : ValueParsing.RoundToStep(e.newValue, low, high);
                slider.SetValueWithoutNotify(value);
                valueLabel.text = ValueParsing.FormatSlider(value, integer);
                if (!Mathf.Approximately(value, get()))
                    set(value);
            });
            control.Add(valueLabel);
            control.Add(slider);
            row.Add(control);
            refresh = () =>
            {
                var value = get();
                slider.SetValueWithoutNotify(value);
                valueLabel.text = ValueParsing.FormatSlider(value, integer);
            };
            refresh();
            return slider;
        }

        public static DropdownField Dropdown(SettingRow row, List<string> labels, Func<int> getIndex, Func<string> currentLabel,
            Action<int> setIndex, VisualElement skinRoot, out Action refresh)
        {
            var dropdown = new DropdownField(labels, -1);
            dropdown.AddToClassList("options-text");
            dropdown.AddToClassList("setting-dropdown");
            dropdown.RegisterCallback<PointerDownEvent>(_ => skinRoot.schedule.Execute(() => SkinOpenDropdown(skinRoot)));
            dropdown.RegisterCallback<NavigationSubmitEvent>(_ => skinRoot.schedule.Execute(() => SkinOpenDropdown(skinRoot)));
            dropdown.RegisterValueChangedCallback(_ =>
            {
                var index = labels.IndexOf(dropdown.value);
                if (index >= 0)
                    setIndex(index);
            });
            row.Add(dropdown);
            refresh = () =>
            {
                var index = getIndex();
                dropdown.SetValueWithoutNotify(index >= 0 ? labels[index] : currentLabel());
            };
            refresh();
            return dropdown;
        }

        /// <summary>Same as OptionsUI.SkinOpenDropdownMenu: the open menu lives outside the document, so give it the screen's sheets.</summary>
        private static void SkinOpenDropdown(VisualElement skinRoot)
        {
            var menu = skinRoot.panel?.visualTree.Q(null, "unity-base-dropdown");
            if (menu == null || menu.ClassListContains("trash-dropdown-skinned"))
                return;
            menu.AddToClassList("trash-dropdown-skinned");
            for (var i = 0; i < skinRoot.styleSheets.count; i++)
                menu.styleSheets.Add(skinRoot.styleSheets[i]);
        }

        public static TextField Text(SettingRow row, Func<string> get, Func<string, bool> commit, out Action refresh)
        {
            var control = new VisualElement();
            control.AddToClassList("setting-control");
            var field = new TextField { isDelayed = true };
            field.AddToClassList("options-text");
            field.style.flexGrow = 1f;
            Action update = () => field.SetValueWithoutNotify(get());
            field.RegisterValueChangedCallback(e =>
            {
                if (!commit(e.newValue))
                    update();
            });
            control.Add(field);
            row.Add(control);
            update();
            refresh = update;
            return field;
        }

        public static Label ReadOnly(SettingRow row, Func<string> text, out Action refresh)
        {
            var control = new VisualElement();
            control.AddToClassList("setting-control");
            var label = new Label();
            label.AddToClassList("options-text");
            label.style.opacity = 0.7f;
            control.Add(label);
            row.Add(control);
            Action update = () => label.text = text();
            update();
            refresh = update;
            return label;
        }

        /// <summary>A short marker after the control, such as "restart needed". Hidden until shown.</summary>
        public static Label Note(SettingRow row, string text)
        {
            var note = new Label(text);
            note.AddToClassList("options-text");
            note.style.opacity = 0.7f;
            note.style.marginLeft = 12f;
            note.style.display = DisplayStyle.None;
            row.Add(note);
            return note;
        }

        public static void Heading(VisualElement parent, string text)
        {
            var heading = new Label(text);
            heading.AddToClassList("options-text");
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginTop = 18f;
            heading.style.marginBottom = 6f;
            parent.Add(heading);
        }

        public static void Message(VisualElement parent, string text)
        {
            var message = new Label(text);
            message.AddToClassList("options-text");
            message.style.marginTop = 24f;
            message.style.unityTextAlign = TextAnchor.MiddleCenter;
            parent.Add(message);
        }
    }
}
```

- [ ] **Step 2: Key rebinding**

`src/ModSettings/KeyRebind.cs`:

```csharp
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Trash;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>
    /// Rebind button for a <see cref="KeyboardShortcut"/>: click, then press a key with any modifiers held.
    /// Esc cancels and Backspace clears. Keys are polled from the Input System keyboard in <see cref="Tick"/>.
    /// While listening, Patches keep Esc from closing Options and the shoulder buttons from changing tab.
    /// </summary>
    internal static class KeyRebind
    {
        private static Button _listening;
        private static Func<object, bool> _set;
        private static Action _restore;
        private static int _startFrame;
        private static int _cancelledFrame = -10;

        public static bool Listening => _listening != null;

        /// <summary>True while listening, and on the frame after one ends, so that frame's Esc/cancel isn't seen by Options too.</summary>
        public static bool ConsumesCancel => _listening != null || Time.frameCount - _cancelledFrame <= 1;

        public static Button Create(SettingRow row, ConfigEntryBase entry, Func<object, bool> set, out Action refresh)
        {
            var control = new VisualElement();
            control.AddToClassList("cycle-control");
            var button = new Button();
            button.AddToClassList("options-text");
            button.AddToClassList("cycle-btn");
            button.AddToClassList("cycle-btn-single");

            Action update = () =>
            {
                if (_listening == button)
                    return;
                var shortcut = (KeyboardShortcut)entry.BoxedValue;
                button.text = shortcut.MainKey == KeyCode.None ? "Not bound" : shortcut.ToString();
            };
            button.clicked += () =>
            {
                if (_listening == button)
                    return;
                Cancel();
                _listening = button;
                _set = set;
                _restore = update;
                _startFrame = Time.frameCount;
                button.text = "Press a key…";
            };
            button.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (_listening == button)
                    Cancel();
            });

            control.Add(button);
            row.Add(control);
            update();
            refresh = update;
            return button;
        }

        public static void Cancel()
        {
            if (_listening == null)
                return;
            var restore = _restore;
            End();
            restore?.Invoke();
        }

        public static void Tick()
        {
            if (_listening == null)
                return;
            if (_listening.panel == null)
            {
                Cancel();
                return;
            }
            var keyboard = Keyboard.current;
            // Skip the frame the button was pressed, so the Enter/Space that pressed it isn't bound.
            if (keyboard == null || Time.frameCount == _startFrame)
                return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Cancel();
                return;
            }

            foreach (var key in keyboard.allKeys)
            {
                if (key == null || !key.wasPressedThisFrame)
                    continue;
                if (!Hotkey.TryGetKeyCode(key.keyCode, out var code) || KeyNames.IsModifier(code.ToString()))
                    continue;

                var set = _set;
                var restore = _restore;
                End();
                set(code == KeyCode.Backspace ? KeyboardShortcut.Empty : new KeyboardShortcut(code, HeldModifiers(keyboard)));
                restore();
                return;
            }
        }

        private static void End()
        {
            _listening = null;
            _set = null;
            _restore = null;
            _cancelledFrame = Time.frameCount;
        }

        private static KeyCode[] HeldModifiers(Keyboard keyboard)
        {
            var modifiers = new List<KeyCode>();
            if (keyboard.leftCtrlKey.isPressed) modifiers.Add(KeyCode.LeftControl);
            if (keyboard.rightCtrlKey.isPressed) modifiers.Add(KeyCode.RightControl);
            if (keyboard.leftShiftKey.isPressed) modifiers.Add(KeyCode.LeftShift);
            if (keyboard.rightShiftKey.isPressed) modifiers.Add(KeyCode.RightShift);
            if (keyboard.leftAltKey.isPressed) modifiers.Add(KeyCode.LeftAlt);
            if (keyboard.rightAltKey.isPressed) modifiers.Add(KeyCode.RightAlt);
            if (keyboard.leftMetaKey.isPressed) modifiers.Add(KeyCode.LeftCommand);
            if (keyboard.rightMetaKey.isPressed) modifiers.Add(KeyCode.RightCommand);
            return modifiers.ToArray();
        }
    }
}
```

- [ ] **Step 3: One control per setting**

`src/ModSettings/SettingEditors.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>Builds one setting's row with the control <see cref="EditorKinds.Choose"/> picks for it.</summary>
    internal static class SettingEditors
    {
        /// <param name="changed">Called after every successful write, so the tab can refresh all rows.</param>
        /// <returns>The row's refresh: re-reads the entry into the control.</returns>
        public static Action Build(VisualElement parent, SettingInfo setting, VisualElement skinRoot, Action changed)
        {
            var entry = setting.Entry;
            var type = entry.SettingType;
            var acceptable = entry.Description?.AcceptableValues;
            var row = Rows.Row(parent, setting.Name, setting.Description);
            var restart = setting.RestartRequired ? Rows.Note(row, "restart needed") : null;

            Func<object, bool> set = value =>
            {
                if (!ConfigWriter.Set(entry, value))
                {
                    changed();
                    return false;
                }
                if (restart != null)
                    restart.style.display = DisplayStyle.Flex;
                changed();
                return true;
            };

            var listValues = ListValues(acceptable);
            var kind = EditorKinds.Choose(type, type == typeof(KeyboardShortcut),
                listValues != null ? Acceptable.List : IsRange(acceptable) ? Acceptable.Range : Acceptable.None,
                listValues?.Count ?? 0);

            Action refresh;
            switch (kind)
            {
                case EditorKind.Toggle:
                    Rows.Cycle(row, () => (bool)entry.BoxedValue ? "On" : "Off",
                        () => set(!(bool)entry.BoxedValue), () => set(!(bool)entry.BoxedValue), out refresh);
                    break;

                case EditorKind.Cycle:
                {
                    var choices = listValues ?? Enum.GetValues(type).Cast<object>().ToList();
                    Rows.Cycle(row, () => Label(entry.BoxedValue, type),
                        () => set(choices[Picker.Wrap(IndexOf(choices, entry.BoxedValue) + 1, choices.Count)]),
                        () => set(choices[Picker.Wrap(IndexOf(choices, entry.BoxedValue) - 1, choices.Count)]),
                        out refresh);
                    break;
                }

                case EditorKind.Dropdown:
                {
                    var choices = listValues ?? Enum.GetValues(type).Cast<object>().ToList();
                    var labels = choices.Select(c => Label(c, type)).ToList();
                    Rows.Dropdown(row, labels, () => IndexOf(choices, entry.BoxedValue), () => Label(entry.BoxedValue, type),
                        i => set(choices[i]), skinRoot, out refresh);
                    break;
                }

                case EditorKind.SliderInt:
                case EditorKind.Slider:
                {
                    var (min, max) = Range(acceptable);
                    var integer = kind == EditorKind.SliderInt;
                    Rows.Slider(row, Convert.ToSingle(min, CultureInfo.InvariantCulture), Convert.ToSingle(max, CultureInfo.InvariantCulture), integer,
                        () => Convert.ToSingle(entry.BoxedValue, CultureInfo.InvariantCulture),
                        v => set(Convert.ChangeType(integer ? ValueParsing.SnapInteger(v) : v, type, CultureInfo.InvariantCulture)),
                        out refresh);
                    break;
                }

                case EditorKind.NumberText:
                    Rows.Text(row, () => ValueParsing.Format(entry.BoxedValue),
                        text => ValueParsing.TryParseNumber(text, type, out var value) && set(value), out refresh);
                    break;

                case EditorKind.Text:
                    Rows.Text(row, () => (string)entry.BoxedValue ?? "", text => set(text), out refresh);
                    break;

                case EditorKind.Key:
                    KeyRebind.Create(row, entry, set, out refresh);
                    break;

                default:
                    Rows.ReadOnly(row, () => $"{entry.GetSerializedValue()}  (edit in the config file)", out refresh);
                    break;
            }

            if (setting.ReadOnly)
                row.SetEnabled(false);
            return refresh;
        }

        /// <summary>A failed row: the setting's name and "can't edit here".</summary>
        public static void BuildBroken(VisualElement parent, SettingInfo setting)
        {
            var row = Rows.Row(parent, setting.Name, setting.Description);
            Rows.ReadOnly(row, () => "can't edit here", out _);
        }

        private static List<object> ListValues(AcceptableValueBase acceptable)
        {
            var type = acceptable?.GetType();
            if (type == null || !type.IsGenericType || type.GetGenericTypeDefinition() != typeof(AcceptableValueList<>))
                return null;
            return ((Array)type.GetProperty("AcceptableValues").GetValue(acceptable)).Cast<object>().ToList();
        }

        private static bool IsRange(AcceptableValueBase acceptable)
        {
            var type = acceptable?.GetType();
            return type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AcceptableValueRange<>);
        }

        private static (object Min, object Max) Range(AcceptableValueBase acceptable)
        {
            var type = acceptable.GetType();
            return (type.GetProperty("MinValue").GetValue(acceptable), type.GetProperty("MaxValue").GetValue(acceptable));
        }

        private static int IndexOf(List<object> choices, object value)
        {
            for (var i = 0; i < choices.Count; i++)
            {
                if (Equals(choices[i], value))
                    return i;
            }
            return -1;
        }

        private static string Label(object value, Type type) =>
            type.IsEnum ? Names.Humanize(value.ToString()) : TomlTypeConverter.ConvertToString(value, type);
    }
}
```

- [ ] **Step 4: The tab model**

`src/ModSettings/ModsTab.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>
    /// The Mods tab's contents: a "Mod" picker, then the selected mod's settings grouped by section, with
    /// advanced ones last. Rebuilt every time Options opens; the picked mod is remembered for the session.
    /// </summary>
    internal sealed class ModsTab
    {
        private static string _rememberedGuid;
        private static readonly HashSet<string> LoggedRows = new HashSet<string>();

        private readonly VisualElement _container;
        private readonly VisualElement _skinRoot;
        private readonly Action<VisualElement, VisualElement> _wireEdges;
        private readonly List<Action> _refreshers = new List<Action>();
        private List<ModInfo> _mods = new List<ModInfo>();
        private ModInfo _selected;
        private Button _picker;

        /// <param name="container">Where rows go (the tab's scroll view).</param>
        /// <param name="skinRoot">The Options document root, whose style sheets open dropdowns borrow.</param>
        /// <param name="wireEdges">Called after each render with the first and last focusable rows (null when empty).</param>
        public ModsTab(VisualElement container, VisualElement skinRoot, Action<VisualElement, VisualElement> wireEdges)
        {
            _container = container;
            _skinRoot = skinRoot;
            _wireEdges = wireEdges;
        }

        public void Rebuild()
        {
            _mods = SettingsRegistry.Collect();
            Render();
        }

        /// <summary>Reset Defaults on the Mods tab: every editable setting of the selected mod.</summary>
        public void ResetSelectedMod()
        {
            if (_selected == null)
                return;
            foreach (var setting in _selected.Settings)
            {
                if (!setting.Hidden && !setting.ReadOnly)
                    ConfigWriter.Set(setting.Entry, setting.Entry.DefaultValue);
            }
            RefreshAll();
        }

        private void Select(int index)
        {
            _rememberedGuid = _mods[Picker.Wrap(index, _mods.Count)].Guid;
            Render();
            _picker?.Focus();
        }

        private void Render()
        {
            KeyRebind.Cancel();
            _container.Clear();
            _refreshers.Clear();
            _picker = null;
            _selected = null;

            if (_mods.Count == 0)
            {
                Rows.Message(_container, "No installed mods have settings.");
                _wireEdges(null, null);
                return;
            }

            var index = Picker.IndexOf(_mods.Select(m => m.Guid).ToList(), _rememberedGuid);
            _selected = _mods[index];
            _rememberedGuid = _selected.Guid;

            var pickerRow = Rows.Row(_container, "Mod", null);
            var mod = _selected;
            _picker = Rows.Cycle(pickerRow, () => $"{mod.Name}  {mod.Version}", () => Select(index + 1), () => Select(index - 1), out _);

            foreach (var group in SettingOrder.Group(_selected.Settings, s => s.Facts))
            {
                Rows.Heading(_container, group.Heading);
                foreach (var setting in group.Items)
                    _refreshers.Add(BuildRow(setting));
            }

            var last = _container.Query<VisualElement>().Where(v => v.focusable && v.canGrabFocus).ToList().LastOrDefault();
            _wireEdges(_picker, last ?? _picker);
        }

        private Action BuildRow(SettingInfo setting)
        {
            var before = _container.childCount;
            try
            {
                return SettingEditors.Build(_container, setting, _skinRoot, RefreshAll);
            }
            catch (Exception e)
            {
                // Drop whatever the failed build added, then show a placeholder row.
                while (_container.childCount > before)
                    _container.RemoveAt(_container.childCount - 1);
                SettingEditors.BuildBroken(_container, setting);
                if (LoggedRows.Add($"{setting.Mod.Guid}/{setting.Entry.Definition}"))
                    Plugin.Log.LogWarning($"Can't show {setting.Mod.Name} / {setting.Entry.Definition} in the Mods tab. {e}");
                return () => { };
            }
        }

        private void RefreshAll()
        {
            foreach (var refresh in _refreshers)
                refresh();
        }
    }
}
```

Note: `Rows.Cycle`'s click handler calls `update()` after `next()`. When `next()` is `Select`, the button has already been detached by `Render`, and updating a detached button's text is harmless.

- [ ] **Step 5: Build**

Run: `dotnet build src/ModSettings/ModSettings.csproj 2>&1 | tail -4`
Expected: `Build succeeded.`, 0 errors.

- [ ] **Step 6: Tests still pass**

Run: `dotnet test tests/ModSettings.Tests/ModSettings.Tests.csproj 2>&1 | tail -2`
Expected: `Failed:     0`.

- [ ] **Step 7: Commit**

```bash
git add src/ModSettings/Rows.cs src/ModSettings/SettingEditors.cs src/ModSettings/KeyRebind.cs src/ModSettings/ModsTab.cs
git commit -m "feat: game-styled setting rows, key rebinding and the Mods tab model

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Options integration and F10

**Files:**
- Create:
  - `src/ModSettings/OptionsAccess.cs`
  - `src/ModSettings/ModsTabInstaller.cs`
  - `src/ModSettings/HotkeyController.cs`
  - `src/ModSettings/Patches/OptionsPatches.cs`
- Modify: `src/ModSettings/Plugin.cs` (Awake: initialise `OptionsAccess` and create the controller)

**Interfaces:**
- Consumes:
  - From Task 1: `HotkeyGate`, `HotkeyState`, `HotkeyAction`
  - From Task 2: `Hotkey.WasPressed`, `Plugin.OpenKey`
  - From Task 3: `ConfigWriter.Tick`, `ConfigWriter.Flush`
  - From Task 4: `KeyRebind`, `ModsTab`
- Game (Assembly-CSharp):
  - `Trash.OptionsUI`, whose private members are used: nested struct `Tab { Button Button; VisualElement Content; Action ResetDefaults; }`, `_tabs`, `_root`, `_currentTab`, `_resetDefaultsBtn`, `ClickTab(Tab)`, `OnTabButtonFocused(Tab)`, `SetDirectionalNavForElement(VisualElement, NavigationMoveEvent.Direction, Func<VisualElement, VisualElement>)` and `CycleTab(int)`
  - `Ox1.MenuScene`: `DefaultFocusElementName`, private `OnNavigationCancel(NavigationCancelEvent)`
  - `Ox1.MenuSceneStack`: `PushActiveScene`, `TryGetActiveScene`
  - `Ox1.MenuSceneManager`: `MenusSuppressed`, `IsStackOpen`, private static `_stack`
  - `Trash.MidgameUI.OptionsScene`, `Trash.MainMenuUI.OptionsScene`, `Trash.LoadingScreenUI.IsVisible`
- Produces:
  - `OptionsAccess.Init() → bool`, `OptionsAccess.Ready`
  - `ModsTabInstaller.ButtonName`, `ModsTabInstaller.Active` (OptionsUI), `ModsTabInstaller.OpenOnModsPending`, `ModsTabInstaller.Install(OptionsUI)`, `ModsTabInstaller.Closed(OptionsUI)`, `ModsTabInstaller.ShowMods(OptionsUI)`
  - `HotkeyController.RestoreFocusName()`

- [ ] **Step 1: Reflection over OptionsUI**

`src/ModSettings/OptionsAccess.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Trash;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>
    /// Every private member of Trash.OptionsUI the Mods tab uses, looked up once. If any is missing (a game
    /// update), <see cref="Ready"/> stays false and the tab is simply not added.
    /// </summary>
    internal static class OptionsAccess
    {
        private static Type _tabType;
        private static FieldInfo _tabButton;
        private static FieldInfo _tabContent;
        private static FieldInfo _tabReset;
        private static FieldInfo _tabs;
        private static FieldInfo _root;
        private static FieldInfo _currentTab;
        private static FieldInfo _resetButton;
        private static MethodInfo _clickTab;
        private static MethodInfo _tabButtonFocused;
        private static MethodInfo _setNav;

        public static bool Ready { get; private set; }

        public static bool Init()
        {
            var ui = typeof(OptionsUI);
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _tabType = ui.GetNestedType("Tab", BindingFlags.NonPublic | BindingFlags.Public);
            _tabButton = _tabType?.GetField("Button", any);
            _tabContent = _tabType?.GetField("Content", any);
            _tabReset = _tabType?.GetField("ResetDefaults", any);
            _tabs = ui.GetField("_tabs", any);
            _root = ui.GetField("_root", any);
            _currentTab = ui.GetField("_currentTab", any);
            _resetButton = ui.GetField("_resetDefaultsBtn", any);
            _clickTab = _tabType == null ? null : ui.GetMethod("ClickTab", any, null, new[] { _tabType }, null);
            _tabButtonFocused = _tabType == null ? null : ui.GetMethod("OnTabButtonFocused", any, null, new[] { _tabType }, null);
            _setNav = ui.GetMethod("SetDirectionalNavForElement", any);

            var missing = new List<string>();
            void Check(object member, string name)
            {
                if (member == null)
                    missing.Add(name);
            }
            Check(_tabType, "Tab");
            Check(_tabButton, "Tab.Button");
            Check(_tabContent, "Tab.Content");
            Check(_tabReset, "Tab.ResetDefaults");
            Check(_tabs, "_tabs");
            Check(_root, "_root");
            Check(_currentTab, "_currentTab");
            Check(_resetButton, "_resetDefaultsBtn");
            Check(_clickTab, "ClickTab");
            Check(_tabButtonFocused, "OnTabButtonFocused");
            Check(_setNav, "SetDirectionalNavForElement");

            Ready = missing.Count == 0;
            if (!Ready)
                Plugin.Log.LogWarning($"The Options screen changed ({string.Join(", ", missing)} not found); the Mods tab is unavailable.");
            return Ready;
        }

        public static VisualElement Root(OptionsUI ui) => (VisualElement)_root.GetValue(ui);

        public static Button ResetButton(OptionsUI ui) => (Button)_resetButton.GetValue(ui);

        /// <summary>A boxed OptionsUI.Tab.</summary>
        public static object MakeTab(Button button, VisualElement content, Action resetDefaults)
        {
            var tab = Activator.CreateInstance(_tabType);
            _tabButton.SetValue(tab, button);
            _tabContent.SetValue(tab, content);
            _tabReset.SetValue(tab, resetDefaults);
            return tab;
        }

        /// <summary>Appends to _tabs unless a tab with the same content is already there.</summary>
        public static void AppendTab(OptionsUI ui, object tab)
        {
            var tabs = (Array)_tabs.GetValue(ui);
            var content = _tabContent.GetValue(tab);
            foreach (var existing in tabs)
            {
                if (ReferenceEquals(_tabContent.GetValue(existing), content))
                    return;
            }
            var bigger = Array.CreateInstance(_tabType, tabs.Length + 1);
            Array.Copy(tabs, bigger, tabs.Length);
            bigger.SetValue(tab, tabs.Length);
            _tabs.SetValue(ui, bigger);
        }

        public static bool IsCurrent(OptionsUI ui, VisualElement content) =>
            ReferenceEquals(_tabContent.GetValue(_currentTab.GetValue(ui)), content);

        public static void ClickTab(OptionsUI ui, object tab) => _clickTab.Invoke(ui, new[] { tab });

        public static void TabButtonFocused(OptionsUI ui, object tab) => _tabButtonFocused.Invoke(ui, new[] { tab });

        public static void SetNav(OptionsUI ui, VisualElement element, NavigationMoveEvent.Direction direction, Func<VisualElement, VisualElement> next) =>
            _setNav.Invoke(ui, new object[] { element, direction, next });
    }
}
```

- [ ] **Step 2: Installing the tab**

`src/ModSettings/ModsTabInstaller.cs`:

```csharp
using System;
using System.Linq;
using Trash;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>
    /// Adds the Mods tab to an Options screen right after OptionsUI.OnEnable rebuilds it. The button and content are
    /// created once per Options document and found by name afterwards; the tab is re-appended to the freshly built
    /// _tabs array each time, and hidden, because OnEnable only resets the game's own four tabs.
    /// </summary>
    internal static class ModsTabInstaller
    {
        public const string ButtonName = "ModsTabBtn";
        public const string ContentName = "ModsTab";

        private static bool _warned;

        /// <summary>The enabled Options screen, if any.</summary>
        public static OptionsUI Active { get; private set; }

        /// <summary>Set by the hotkey around PushActiveScene so the tab opens on Mods.</summary>
        public static bool OpenOnModsPending;

        public static void Install(OptionsUI ui)
        {
            Active = ui;
            if (!OptionsAccess.Ready)
                return;

            var root = OptionsAccess.Root(ui);
            var contentArea = root?.Q<VisualElement>("OptionsPanel")?.Q<VisualElement>(null, "content-area") ?? root;
            var keybindsButton = root?.Q<Button>("KeybindsTabBtn");
            var audioTab = contentArea?.Q<VisualElement>("AudioTab");
            if (root == null || keybindsButton == null || audioTab == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    Plugin.Log.LogWarning("The Options screen layout changed (KeybindsTabBtn or AudioTab not found); the Mods tab is unavailable.");
                }
                return;
            }

            var button = root.Q<Button>(ButtonName) ?? CreateButton(ui, keybindsButton);
            var content = contentArea.Q<VisualElement>(ContentName) ?? CreateContent(audioTab);
            if (button.userData == null)
            {
                button.userData = OptionsAccess.MakeTab(button, content, () => (content.userData as ModsTab)?.ResetSelectedMod());
                WireButton(ui, button, content);
            }
            var tab = button.userData;
            OptionsAccess.AppendTab(ui, tab);

            // OnEnable reset only the game's tabs; ours could still be showing from last time.
            content.style.display = DisplayStyle.None;
            button.RemoveFromClassList("tab-button--active");
            button.parent?.RemoveFromClassList("tab-button-container--active");

            if (!(content.userData is ModsTab model))
            {
                var scroll = content.Q<ScrollView>();
                model = new ModsTab(scroll, root, (first, last) =>
                {
                    if (first != null)
                        OptionsAccess.SetNav(ui, first, NavigationMoveEvent.Direction.Up, _ => button);
                    if (last != null)
                        OptionsAccess.SetNav(ui, last, NavigationMoveEvent.Direction.Down, _ => OptionsAccess.ResetButton(ui));
                });
                content.userData = model;
            }
            model.Rebuild();

            if (OpenOnModsPending)
            {
                OpenOnModsPending = false;
                OptionsAccess.ClickTab(ui, tab);
            }
        }

        public static void Closed(OptionsUI ui)
        {
            KeyRebind.Cancel();
            ConfigWriter.Flush();
            HotkeyController.RestoreFocusName();
            if (Active == ui)
                Active = null;
        }

        public static void ShowMods(OptionsUI ui)
        {
            var tab = OptionsAccess.Root(ui)?.Q<Button>(ButtonName)?.userData;
            if (tab != null)
                OptionsAccess.ClickTab(ui, tab);
        }

        private static Button CreateButton(OptionsUI ui, Button template)
        {
            var button = new Button { name = ButtonName };
            CopyClasses(template, button, "tab-button--active");
            if (!string.IsNullOrEmpty(template.text))
            {
                button.text = "Mods";
            }
            else
            {
                // The game's tab buttons may draw their caption with a child label instead of Button.text.
                var templateLabel = template.Q<Label>();
                var label = new Label("Mods");
                if (templateLabel != null)
                    CopyClasses(templateLabel, label);
                button.Add(label);
            }

            var container = template.parent;
            var ownContainer = container != null && container.parent != null && container.Query<Button>().ToList().Count == 1;
            if (ownContainer)
            {
                var newContainer = new VisualElement();
                CopyClasses(container, newContainer, "tab-button-container--active", "tab-button-container--focused");
                newContainer.Add(button);
                container.parent.Insert(container.parent.IndexOf(container) + 1, newContainer);
            }
            else if (container != null)
            {
                container.Insert(container.IndexOf(template) + 1, button);
            }
            return button;
        }

        private static void WireButton(OptionsUI ui, Button button, VisualElement content)
        {
            button.clicked += () => OptionsAccess.ClickTab(ui, button.userData);
            button.RegisterCallback<FocusInEvent>(_ =>
            {
                OptionsAccess.TabButtonFocused(ui, button.userData);
                button.parent?.AddToClassList("tab-button-container--focused");
            });
            button.RegisterCallback<FocusOutEvent>(_ => button.parent?.RemoveFromClassList("tab-button-container--focused"));
            OptionsAccess.SetNav(ui, button, NavigationMoveEvent.Direction.Up, _ => Focusables(content).LastOrDefault());
            OptionsAccess.SetNav(ui, button, NavigationMoveEvent.Direction.Down, _ => Focusables(content).FirstOrDefault());
        }

        private static VisualElement CreateContent(VisualElement template)
        {
            var content = new VisualElement { name = ContentName };
            CopyClasses(template, content);
            content.style.display = DisplayStyle.None;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.flexGrow = 1f;
            // Keep the focused row on screen when moving with a controller or the keyboard.
            scroll.RegisterCallback<FocusInEvent>(e =>
            {
                if (e.target is VisualElement target && target != scroll)
                    scroll.ScrollTo(target);
            }, TrickleDown.TrickleDown);
            content.Add(scroll);

            template.parent.Insert(template.parent.IndexOf(template) + 1, content);
            return content;
        }

        private static System.Collections.Generic.List<VisualElement> Focusables(VisualElement container) =>
            container.Query<VisualElement>().Where(v => v.focusable && v.canGrabFocus).ToList();

        /// <summary>Copies USS classes except Unity's own (unity-*) and any listed state classes.</summary>
        private static void CopyClasses(VisualElement from, VisualElement to, params string[] except)
        {
            foreach (var cls in from.GetClasses())
            {
                if (cls.StartsWith("unity-", StringComparison.Ordinal) || Array.IndexOf(except, cls) >= 0)
                    continue;
                to.AddToClassList(cls);
            }
        }
    }
}
```

- [ ] **Step 3: Patches**

`src/ModSettings/Patches/OptionsPatches.cs`:

```csharp
using System;
using HarmonyLib;
using Ox1;
using Trash;

namespace ModSettings.Patches
{
    [HarmonyPatch(typeof(OptionsUI), "OnEnable")]
    internal static class OptionsEnablePatch
    {
        private static bool _logged;

        private static void Postfix(OptionsUI __instance)
        {
            try
            {
                ModsTabInstaller.Install(__instance);
            }
            catch (Exception e)
            {
                if (!_logged)
                {
                    _logged = true;
                    Plugin.Log.LogError($"Couldn't add the Mods tab; Options is unchanged. {e}");
                }
            }
        }
    }

    [HarmonyPatch(typeof(OptionsUI), "OnDisable")]
    internal static class OptionsDisablePatch
    {
        private static void Postfix(OptionsUI __instance)
        {
            try
            {
                ModsTabInstaller.Closed(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Mods tab close failed. {e}");
            }
        }
    }

    /// <summary>The shoulder buttons don't change tab while a key rebind is listening.</summary>
    [HarmonyPatch(typeof(OptionsUI), "CycleTab")]
    internal static class OptionsCycleTabPatch
    {
        private static bool Prefix() => !KeyRebind.Listening;
    }

    /// <summary>Esc cancels a listening key rebind instead of closing the screen.</summary>
    [HarmonyPatch(typeof(MenuScene), "OnNavigationCancel")]
    internal static class MenuSceneCancelPatch
    {
        private static bool Prefix() => !KeyRebind.ConsumesCancel;
    }
}
```

- [ ] **Step 4: F10 controller**

`src/ModSettings/HotkeyController.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using Ox1;
using Trash;
using UnityEngine;
using UnityEngine.UIElements;

namespace ModSettings
{
    /// <summary>
    /// Per-frame work: key rebinding, batched saves and the open key. The key opens the pause menu's (in a run)
    /// or title screen's Options scene on the Mods tab, or switches to Mods when Options is already open.
    /// </summary>
    internal sealed class HotkeyController : MonoBehaviour
    {
        private static readonly FieldInfo OpenStacks =
            typeof(MenuSceneManager).GetField("_stack", BindingFlags.Static | BindingFlags.NonPublic);

        private static MenuScene _focusSwapped;
        private static string _originalFocusName;

        private bool _loggedError;

        private void Update()
        {
            try
            {
                KeyRebind.Tick();
                ConfigWriter.Tick(Time.unscaledTime);
                if (Hotkey.WasPressed(Plugin.OpenKey.Value))
                    OnOpenKey();
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Plugin.Log.LogError($"Mod settings update failed. {e}");
                }
            }
        }

        private void OnDestroy()
        {
            ConfigWriter.Flush();
            RestoreFocusName();
        }

        private static void OnOpenKey()
        {
            FindHost(out var stack, out var hostScene, out var optionsScene);
            var options = ModsTabInstaller.Active;
            var state = new HotkeyState
            {
                OptionsActive = options != null && options.isActiveAndEnabled,
                TextFieldFocused = TextFieldFocused(options),
                Rebinding = KeyRebind.Listening,
                Loading = LoadingScreenUI.IsVisible,
                MenusSuppressed = MenuSceneManager.MenusSuppressed,
                HasMenuHost = stack != null && optionsScene != null,
                OtherMenuOpen = OtherMenuOpen(stack, hostScene, optionsScene),
            };

            switch (HotkeyGate.Decide(state))
            {
                case HotkeyAction.ShowModsTab:
                    ModsTabInstaller.ShowMods(options);
                    break;
                case HotkeyAction.OpenOptions:
                    Open(stack, optionsScene);
                    break;
            }
        }

        /// <summary>The pause menu in a run, otherwise the title screen.</summary>
        private static void FindHost(out MenuSceneStack stack, out MenuScene hostScene, out MenuScene optionsScene)
        {
            stack = null;
            hostScene = null;
            optionsScene = null;
            var midgame = FindFirstObjectByType<MidgameUI>(FindObjectsInactive.Include);
            if (midgame != null)
            {
                optionsScene = midgame.OptionsScene;
                hostScene = midgame.GetComponentInParent<MenuScene>(true);
                stack = midgame.GetComponentInParent<MenuSceneStack>(true);
                return;
            }
            var title = FindFirstObjectByType<MainMenuUI>(FindObjectsInactive.Include);
            if (title != null)
            {
                optionsScene = title.OptionsScene;
                hostScene = title.GetComponentInParent<MenuScene>(true);
                stack = title.GetComponentInParent<MenuSceneStack>(true);
            }
        }

        /// <summary>Any open menu stack other than the host's, or the host's showing something besides itself or Options.</summary>
        private static bool OtherMenuOpen(MenuSceneStack stack, MenuScene hostScene, MenuScene optionsScene)
        {
            if (OpenStacks?.GetValue(null) is List<MenuSceneStack> open)
            {
                foreach (var other in open)
                {
                    if (other != stack)
                        return true;
                }
            }
            if (stack != null && MenuSceneManager.IsStackOpen(stack) && stack.TryGetActiveScene(out var active))
                return active != hostScene && active != optionsScene;
            return false;
        }

        private static bool TextFieldFocused(OptionsUI options)
        {
            if (options == null)
                return false;
            var focused = OptionsAccess.Ready ? OptionsAccess.Root(options)?.panel?.focusController?.focusedElement as VisualElement : null;
            return focused is TextField || focused?.GetFirstAncestorOfType<TextField>() != null;
        }

        /// <summary>
        /// Points the scene's deferred default focus at the Mods tab button: otherwise MenuScene focuses the Gameplay
        /// tab button a frame later, which switches back to Gameplay. Restored when Options closes.
        /// </summary>
        private static void Open(MenuSceneStack stack, MenuScene optionsScene)
        {
            RestoreFocusName();
            _focusSwapped = optionsScene;
            _originalFocusName = optionsScene.DefaultFocusElementName;
            optionsScene.DefaultFocusElementName = ModsTabInstaller.ButtonName;
            ModsTabInstaller.OpenOnModsPending = true;
            try
            {
                stack.PushActiveScene(optionsScene);
            }
            finally
            {
                ModsTabInstaller.OpenOnModsPending = false;
            }
        }

        internal static void RestoreFocusName()
        {
            if (_focusSwapped == null)
                return;
            _focusSwapped.DefaultFocusElementName = _originalFocusName;
            _focusSwapped = null;
            _originalFocusName = null;
        }
    }
}
```

- [ ] **Step 5: Wire the plugin**

In `src/ModSettings/Plugin.cs`, replace the final log line of `Awake`, `Log.LogInfo($"{PluginName} {PluginVersion} loaded");`, with:

```csharp
            OptionsAccess.Init();

            // Its own object, so it outlives scene loads and doesn't depend on BepInEx's manager object staying alive.
            var host = new GameObject("ModSettings");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<HotkeyController>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded; press {OpenKey.Value} for the Mods tab");
```

- [ ] **Step 6: Check the private members exist in this game build**

Run: `grep -nE "private (struct Tab|Tab\[\] _tabs|VisualElement _root|Tab _currentTab|Button _resetDefaultsBtn|void ClickTab|void OnTabButtonFocused|void SetDirectionalNavForElement|void CycleTab)" /tmp/trashday-src/Trash/OptionsUI.cs | wc -l; grep -c "private void OnNavigationCancel" /tmp/trashday-src/Ox1/MenuScene.cs; grep -c "private static readonly List<MenuSceneStack> _stack" /tmp/trashday-src/Ox1/MenuSceneManager.cs`
Expected: `9`, then `1`, then `1`.

- [ ] **Step 7: Build**

Run: `dotnet build src/ModSettings/ModSettings.csproj 2>&1 | tail -4`
Expected: `Build succeeded.`, 0 errors, and the DLL deployed.

- [ ] **Step 8: Tests still pass**

Run: `dotnet test tests/ModSettings.Tests/ModSettings.Tests.csproj 2>&1 | tail -2`
Expected: `Failed:     0`.

- [ ] **Step 9: Commit**

```bash
git add src/ModSettings/OptionsAccess.cs src/ModSettings/ModsTabInstaller.cs src/ModSettings/HotkeyController.cs src/ModSettings/Patches/OptionsPatches.cs src/ModSettings/Plugin.cs
git commit -m "feat: Mods tab in the game's Options screen and the F10 key

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Reference assemblies, packaging, release workflow and docs

**Files:**
- Create:
  - `scripts/update-refs.sh`, `scripts/package.sh`, `scripts/thunderstore-publish.sh` (copied), `lib/refs/*.dll` (generated)
  - `.github/workflows/release.yml`
  - `thunderstore/manifest.json`, `thunderstore/README.md`, `thunderstore/icon.svg`, `thunderstore/icon.png`
  - `README.md`, `CHANGELOG.md`, `CLAUDE.md`, `docs/manual-verification-0.1.0.md`

**Interfaces:**
- Consumes: the built `src/ModSettings/bin/Release/ModSettings.dll`.
- Produces: `dist/darkharasho-ModSettings-0.1.0.zip`.

- [ ] **Step 1: Scripts**

Copy the publish script: `cp ~/.claude/templates/thunderstore/thunderstore-publish.sh scripts/ && chmod +x scripts/thunderstore-publish.sh`.

`scripts/update-refs.sh`:

```bash
#!/usr/bin/env bash
# Regenerates lib/refs: reference-only copies (public API signatures, no method bodies) of the
# game and BepInEx assemblies, so CI can build without the game installed.
# Needs the refasmer tool: dotnet tool install -g JetBrains.Refasmer.CliTool
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
game_root="${GAME_PATH:-/var/mnt/data/SteamLibrary/steamapps/common/TrashDay}"
game="$game_root/Trash_Data/Managed"
# Any BepInEx 5.4 core works. Falls back to the Stonewards r2modman profile if BepInEx isn't in the game folder.
bepinex="${BEPINEX_CORE:-$game_root/BepInEx/core}"
[ -f "$bepinex/BepInEx.dll" ] || bepinex="$HOME/.config/r2modmanPlus-local/Stonewards/profiles/Default/BepInEx/core"
out="$root/lib/refs"

# Keep in sync with the <Reference> items in src/ModSettings/ModSettings.csproj.
game_dlls=(Assembly-CSharp UnityEngine UnityEngine.CoreModule UnityEngine.TextRenderingModule UnityEngine.UIElementsModule Unity.InputSystem)
bepinex_dlls=(BepInEx 0Harmony)

rm -rf "$out"
mkdir -p "$out"
for name in "${game_dlls[@]}"; do refasmer -q -c --omit-non-api-members=true -O "$out" "$game/$name.dll"; done
for name in "${bepinex_dlls[@]}"; do refasmer -q -c --omit-non-api-members=true -O "$out" "$bepinex/$name.dll"; done
ls -l "$out"
```

`scripts/package.sh`:

```bash
#!/usr/bin/env bash
# Builds a Release DLL and zips a Thunderstore/r2modman package into dist/.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
manifest="$root/thunderstore/manifest.json"
stage="$root/dist/stage"

# Validate the manifest and check the version matches the plugin and project.
version="$(python3 - "$manifest" "$root" <<'PY'
import json, re, sys, pathlib
m = json.load(open(sys.argv[1]))
root = pathlib.Path(sys.argv[2])
errors = []
if not re.fullmatch(r"[A-Za-z0-9_]+", m.get("name", "")):
    errors.append("name must be letters, digits and underscores")
if not re.fullmatch(r"\d+\.\d+\.\d+", m.get("version_number", "")):
    errors.append("version_number must be Major.Minor.Patch")
if len(m.get("description", "")) > 250:
    errors.append("description is over 250 characters")
v = m.get("version_number")
plugin = re.search(r'PluginVersion = "([^"]+)"', (root / "src/ModSettings/Plugin.cs").read_text())
csproj = re.search(r"<Version>([^<]+)</Version>", (root / "src/ModSettings/ModSettings.csproj").read_text())
if not plugin or plugin.group(1) != v:
    errors.append(f"Plugin.cs PluginVersion is {plugin and plugin.group(1)}, manifest is {v}")
if not csproj or csproj.group(1) != v:
    errors.append(f"ModSettings.csproj Version is {csproj and csproj.group(1)}, manifest is {v}")
if f"## {v}" not in (root / "CHANGELOG.md").read_text():
    errors.append(f"CHANGELOG.md has no '## {v}' entry")
if errors:
    sys.exit("manifest check failed:\n  " + "\n  ".join(errors))
print(v)
PY
)"
zipfile="$root/dist/darkharasho-ModSettings-$version.zip"

dotnet build "$root/src/ModSettings/ModSettings.csproj" -c Release

rm -rf "$stage" "$zipfile"
mkdir -p "$stage/plugins"
cp "$manifest" "$root/thunderstore/icon.png" "$root/thunderstore/README.md" "$root/CHANGELOG.md" "$stage/"
cp "$root/src/ModSettings/bin/Release/ModSettings.dll" "$stage/plugins/"

(cd "$stage" && zip -qrX "$zipfile" manifest.json icon.png README.md CHANGELOG.md plugins)
rm -rf "$stage"
unzip -l "$zipfile"
echo "Package: $zipfile"
```

Run `chmod +x scripts/*.sh`, then `scripts/update-refs.sh`.
Expected: `lib/refs` lists 8 DLLs: Assembly-CSharp, UnityEngine, UnityEngine.CoreModule, UnityEngine.TextRenderingModule, UnityEngine.UIElementsModule, Unity.InputSystem, BepInEx and 0Harmony.

- [ ] **Step 2: Build against the refs (the CI path)**

Run: `dotnet build src/ModSettings/ModSettings.csproj -p:UseRefs=true -o /tmp/modsettings-refs-build 2>&1 | tail -3`
Expected: `Build succeeded.`, 0 errors. Because of `-o`, the game copy is untouched by this build. The deploy target still copies, which is harmless since it's the same code.

- [ ] **Step 3: Thunderstore files**

`thunderstore/manifest.json`:

```json
{
    "name": "ModSettings",
    "version_number": "0.1.0",
    "website_url": "https://github.com/darkharasho/trashday-mod-settings",
    "description": "In-game settings for every Trash Day mod: a Mods tab in Options (or press F10) with toggles, sliders, choices, text and key rebinding. No config file editing.",
    "dependencies": [
        "BepInEx-BepInExPack-5.4.2305"
    ]
}
```

`thunderstore/README.md`:

```markdown
# Mod Settings

Change any mod's settings inside Trash Day. Open **Options → Mods**, or press **F10** anywhere.

## How it works
- The **Mod** row at the top picks which mod to show; ◀ ▶ (or click) to switch.
- Each setting gets a control that fits it: On/Off, a choice, a slider, a text box, or a key you can rebind.
- Changes apply straight away and are saved to the mod's config file.
- **Reset Defaults** on the Mods tab resets the selected mod.
- Works with any BepInEx mod's settings; mods don't need to do anything to appear.

## Notes
- Esc cancels a key rebind; Backspace unbinds the key.
- Some settings say "restart needed" after you change them; they take effect next launch.
- F10 is itself a setting under Mod Settings, so you can rebind it there.
```

`thunderstore/icon.svg`:

```xml
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#3a3f44"/>
      <stop offset="1" stop-color="#1c1f22"/>
    </linearGradient>
    <linearGradient id="can" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#3f8f4a"/>
      <stop offset="0.5" stop-color="#5cb866"/>
      <stop offset="1" stop-color="#2f6e38"/>
    </linearGradient>
    <linearGradient id="gold" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#ffe08a"/>
      <stop offset="1" stop-color="#d9a92e"/>
    </linearGradient>
  </defs>
  <rect width="256" height="256" rx="28" fill="url(#bg)"/>
  <rect x="62" y="58" width="132" height="20" rx="6" fill="#2f6e38"/>
  <rect x="108" y="44" width="40" height="16" rx="5" fill="#2f6e38"/>
  <path d="M72 84 H184 L172 214 Q170 224 160 224 H96 Q86 224 84 214 Z" fill="url(#can)"/>
  <g stroke="#2a5f31" stroke-width="6" stroke-linecap="round">
    <line x1="104" y1="104" x2="108" y2="204"/>
    <line x1="128" y1="104" x2="128" y2="204"/>
    <line x1="152" y1="104" x2="148" y2="204"/>
  </g>
  <g transform="translate(128 150)" stroke="#5a4310" stroke-width="3" fill="url(#gold)">
    <g>
      <rect x="-9" y="-62" width="18" height="22" rx="3"/>
      <rect x="-9" y="-62" width="18" height="22" rx="3" transform="rotate(45)"/>
      <rect x="-9" y="-62" width="18" height="22" rx="3" transform="rotate(90)"/>
      <rect x="-9" y="-62" width="18" height="22" rx="3" transform="rotate(135)"/>
      <rect x="-9" y="-62" width="18" height="22" rx="3" transform="rotate(180)"/>
      <rect x="-9" y="-62" width="18" height="22" rx="3" transform="rotate(225)"/>
      <rect x="-9" y="-62" width="18" height="22" rx="3" transform="rotate(270)"/>
      <rect x="-9" y="-62" width="18" height="22" rx="3" transform="rotate(315)"/>
    </g>
    <circle r="46"/>
    <circle r="18" fill="#2f6e38"/>
  </g>
</svg>
```

Run: `rsvg-convert -w 256 -h 256 thunderstore/icon.svg -o thunderstore/icon.png && python3 -c "import struct;d=open('thunderstore/icon.png','rb').read(24);print(struct.unpack('>II',d[16:24]))"`
Expected: `(256, 256)`. If `rsvg-convert` is missing, install it with `brew install librsvg`. Then look at the PNG with the Read tool: a gold gear over a green trash can.

- [ ] **Step 4: Release workflow**

`.github/workflows/release.yml`:

```yaml
name: Release

on:
  push:
    tags: ["v*"]

permissions:
  contents: write

jobs:
  release:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Check tag matches manifest version
        run: |
          version="$(jq -r .version_number thunderstore/manifest.json)"
          if [ "v$version" != "$GITHUB_REF_NAME" ]; then
            echo "::error::Tag $GITHUB_REF_NAME does not match manifest version $version"
            exit 1
          fi
          echo "VERSION=$version" >> "$GITHUB_ENV"

      - name: Test
        run: dotnet test tests/ModSettings.Tests/ModSettings.Tests.csproj

      # The game isn't installed here, so the build uses the reference assemblies in lib/refs.
      - name: Build and package
        run: scripts/package.sh

      - name: Release notes from changelog
        run: |
          awk -v v="## $VERSION" '$0 == v {on=1; next} /^## / {on=0} on' CHANGELOG.md > notes.md
          cat >> notes.md <<EOF

          For Trash Day. Install with r2modman, or download \`darkharasho-ModSettings-$VERSION.zip\` below and use **Settings → Import local mod**. Requires BepInExPack 5.4.2305. Open **Options → Mods** or press F10.
          EOF

      - name: Create or update GitHub release
        env:
          GH_TOKEN: ${{ github.token }}
        run: |
          zip="dist/darkharasho-ModSettings-$VERSION.zip"
          if gh release view "$GITHUB_REF_NAME" >/dev/null 2>&1; then
            gh release upload "$GITHUB_REF_NAME" "$zip" --clobber
            # Deleting a tag to re-push it turns its release into a draft; publish it again.
            gh release edit "$GITHUB_REF_NAME" --draft=false
          else
            gh release create "$GITHUB_REF_NAME" "$zip" --title "ModSettings $VERSION" --notes-file notes.md
          fi

      # Skips versions already on Thunderstore, so re-pushing a tag is safe. Thunderstore has no Trash Day
      # community yet; "trash-day" is a placeholder slug. Fix it, add --categories and drop continue-on-error
      # once the community exists.
      - name: Publish to Thunderstore
        continue-on-error: true
        env:
          THUNDERSTORE_TOKEN: ${{ secrets.THUNDERSTORE_TOKEN }}
        run: scripts/thunderstore-publish.sh "dist/darkharasho-ModSettings-$VERSION.zip" --community trash-day

      - uses: actions/upload-artifact@v4
        with:
          name: darkharasho-ModSettings-${{ env.VERSION }}
          path: dist/*.zip
```

- [ ] **Step 5: Docs**

`CHANGELOG.md`:

```markdown
# Changelog

## 0.1.0

- First release: a Mods tab in Options (and F10 to jump to it) for every BepInEx mod's settings, with On/Off, choice, slider, text and key-rebind controls, Reset Defaults per mod, and batched saves.
```

`README.md`:

````markdown
# trashday-mod-settings

One in-game settings screen for every BepInEx mod in Trash Day: a **Mods** tab in the game's Options screen, and F10 to jump straight to it. Packaged for Thunderstore as `darkharasho-ModSettings`; the store page is `thunderstore/README.md`.

## How it works

- `Patches/OptionsPatches.cs`: after `OptionsUI.OnEnable` rebuilds the Options screen, `ModsTabInstaller` adds a Mods tab button and content and appends a fifth `OptionsUI.Tab` to the game's private tab list, so shoulder-button cycling, navigation and Reset Defaults include it. `OptionsAccess` holds every reflected member; if one is missing, the tab is skipped and Options is untouched.
- `ModsTab` shows a Mod picker and the picked mod's settings grouped by section (advanced last). `SettingsRegistry` reads every plugin's config, honouring ConfigurationManagerAttributes-style tags. `SettingEditors` picks a control per setting type and builds it with the game's own USS classes (`Rows`).
- `ConfigWriter` writes through `BoxedValue` (mods get `SettingChanged` immediately) and saves at most once a second, plus on close.
- `HotkeyController` handles F10 (opening `MidgameUI.OptionsScene` or `MainMenuUI.OptionsScene` on the Mods tab), key rebinding and the save timer.

## Development

- `dotnet test tests/ModSettings.Tests/ModSettings.Tests.csproj` runs the unit tests (pure logic only).
- `dotnet build src/ModSettings/ModSettings.csproj` builds and copies the DLL into the game's `BepInEx/plugins/darkharasho-ModSettings`.
- `scripts/package.sh` builds Release and writes `dist/darkharasho-ModSettings-<version>.zip`; it fails if the manifest, `Plugin.cs`, csproj and `CHANGELOG.md` versions disagree.
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
````

`CLAUDE.md`:

```markdown
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
```

`docs/manual-verification-0.1.0.md`:

```markdown
# Manual verification, 0.1.0

Deploy the build (More Players and Admin Menu installed too). Tick each line.

## The tab
- [ ] Title screen → Options: a **Mods** tab follows Keybinds, styled like the others.
- [ ] Close and reopen Options twice: still exactly one Mods tab; Options opens on Gameplay and Mods content isn't showing under it.
- [ ] Shoulder buttons / tab-cycle keys cycle Gameplay → … → Keybinds → Mods → Gameplay.
- [ ] Controller/arrow keys: down from the Mods tab button reaches the Mod picker; down from the last row reaches Reset Defaults; up from the picker returns to the tab button.
- [ ] Pause menu → Options shows the same Mods tab.

## Contents
- [ ] The Mod picker lists Admin Menu, Mod Settings and More Players; ◀ ▶ (left/right or click) wraps around.
- [ ] More Players: "Max players" is a slider with a whole-number label; dragging it updates the label; after closing Options, `BepInEx/config/*moreplayers*.cfg` shows the new value.
- [ ] Admin Menu: "Toggle key" shows F1. Click it, press F2: it shows F2 and F2 now opens the admin menu (as host in a level).
- [ ] Click it again and press Esc: the rebind cancels, the label returns, and Options stays open.
- [ ] Click it and press Backspace: shows "Not bound".
- [ ] Admin Menu's on/off settings cycle On/Off.
- [ ] Reset Defaults on the Mods tab asks for confirmation, then resets only the selected mod.
- [ ] Mod Settings: "Open key" shows F10.
- [ ] Descriptions show as a smaller line under each name; sections have headings.

## F10
- [ ] Title screen: F10 opens Options on the Mods tab. Back returns to the title screen.
- [ ] In a run (pause menu closed): F10 opens Options on Mods; Back goes to the pause menu; Esc/Resume back to play.
- [ ] With the pause menu open: F10 opens Options on Mods.
- [ ] With Options open on Gameplay: F10 switches to Mods.
- [ ] With the Reset Defaults confirmation open: F10 does nothing.
- [ ] With the Admin Menu open (F1): F10 does nothing.
- [ ] On a loading screen: F10 does nothing.

## Persistence
- [ ] Change More Players' max players and Admin Menu's key, quit the game, relaunch: both changes are still there.
```

- [ ] **Step 6: Package**

Run: `scripts/package.sh 2>&1 | tail -10`
Expected: `unzip -l` lists `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md` and `plugins/ModSettings.dll`, then `Package: …/dist/darkharasho-ModSettings-0.1.0.zip`.

- [ ] **Step 7: Rebuild Debug into the game** (packaging built Release; this makes sure the deployed DLL is current)

Run: `dotnet build src/ModSettings/ModSettings.csproj 2>&1 | grep -E "Deployed|error" | head -3`
Expected: one `Deployed ModSettings.dll` line and no errors.

- [ ] **Step 8: Commit**

```bash
git add scripts lib/refs .github thunderstore README.md CHANGELOG.md CLAUDE.md docs/manual-verification-0.1.0.md
git commit -m "build: reference assemblies, Thunderstore package, release workflow and docs

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
