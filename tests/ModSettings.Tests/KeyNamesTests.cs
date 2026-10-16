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
