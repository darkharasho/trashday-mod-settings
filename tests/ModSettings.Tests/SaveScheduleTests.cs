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
