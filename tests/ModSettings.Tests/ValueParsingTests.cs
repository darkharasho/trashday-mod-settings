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
