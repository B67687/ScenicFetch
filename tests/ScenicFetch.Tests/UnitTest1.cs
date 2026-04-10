namespace ScenicFetch.Tests;

public class UnitTest1
{
    [Fact]
    public void SourceIdParsing_RecognizesCliNames()
    {
        Assert.True(ScenicFetch.Core.SourceIdExtensions.TryParse("apple-aerial", out var sourceId));
        Assert.Equal(ScenicFetch.Core.SourceId.AppleAerial, sourceId);
    }
}
