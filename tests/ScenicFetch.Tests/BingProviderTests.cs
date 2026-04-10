using ScenicFetch.Providers;

namespace ScenicFetch.Tests;

public sealed class BingProviderTests
{
    [Fact]
    public void MapImages_BuildsExpectedUrlsAndMetadata()
    {
        var response = new BingResponse(
            [
                new BingImage(
                    "Frozen Lake",
                    "Photo by Example",
                    "/travel",
                    "20260410",
                    "/th?id=OHR.FrozenLake")
            ]);

        var items = BingProvider.MapImages(response, "UHD");
        var item = Assert.Single(items);

        Assert.Equal("https://www.bing.com/th?id=OHR.FrozenLake_UHD.jpg", item.PrimaryUrl);
        Assert.Equal("https://www.bing.com/th?id=OHR.FrozenLake_768x1366.jpg", item.PreviewUrl);
        Assert.Contains(item.Variants, variant => variant.Name == "1080x1920");
        Assert.Equal("2026-04-10", item.CapturedAt?.UtcDateTime.ToString("yyyy-MM-dd"));
        Assert.Equal("https://www.bing.com/travel", item.PageUrl);
    }
}
