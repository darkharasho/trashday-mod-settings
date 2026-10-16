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
