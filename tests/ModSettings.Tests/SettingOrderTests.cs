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
