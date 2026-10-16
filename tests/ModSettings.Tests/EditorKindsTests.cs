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
    [InlineData(2, false)]
    [InlineData(6, false)]
    [InlineData(7, true)]
    public void ListsCycleUpToSixThenDrop(int count, bool dropdown)
    {
        var expected = dropdown ? EditorKind.Dropdown : EditorKind.Cycle;
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
