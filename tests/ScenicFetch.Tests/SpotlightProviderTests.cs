using ScenicFetch.Providers;

namespace ScenicFetch.Tests;

public sealed class SpotlightProviderTests
{
    [Fact]
    public void MapItems_UsesSelectedOrientationAndNestedPayload()
    {
        const string nestedItem =
            """
            {
              "ad": {
                "title": "Norwegian Fjord",
                "description": "Cliffs and water.",
                "copyright": "Example Photographer",
                "ctaUri": "microsoft-edge:https://example.com/story",
                "entityId": "spotlight-123",
                "portraitImage": { "asset": "https://example.com/portrait.jpg" },
                "landscapeImage": { "asset": "https://example.com/landscape.jpg" }
              }
            }
            """;

        var page = new SpotlightPageResponse(
            new SpotlightBatchResponse([new SpotlightBatchItem(nestedItem)]));

        var items = SpotlightProvider.MapItems(page, "landscape", "US");
        var item = Assert.Single(items);

        Assert.Equal("spotlight-123", item.Id);
        Assert.Equal("https://example.com/landscape.jpg", item.PrimaryUrl);
        Assert.Equal("https://example.com/story", item.PageUrl);
        Assert.Contains(item.Variants, variant => variant.Name == "portrait");
        Assert.Contains("US", item.Tags);
    }
}
