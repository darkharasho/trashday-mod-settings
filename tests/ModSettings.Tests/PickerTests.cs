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
