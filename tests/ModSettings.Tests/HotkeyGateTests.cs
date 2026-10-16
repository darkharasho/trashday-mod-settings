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
