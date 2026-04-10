using System.Formats.Tar;
using System.Text;
using ScenicFetch.Providers;

namespace ScenicFetch.Tests;

public sealed class AppleAerialProviderTests
{
    [Fact]
    public async Task ParseCatalogArchiveAsync_ReadsEntriesJsonFromTar()
    {
        const string json =
            """
            {
              "categories": [
                {
                  "id": "LAND",
                  "localizedNameKey": "AerialCategoryLandscapes",
                  "subcategories": [
                    { "id": "YOSE", "localizedNameKey": "AerialSubcategoryYosemite" }
                  ]
                }
              ],
              "assets": [
                {
                  "id": "asset-1",
                  "accessibilityLabel": "Yosemite Dawn",
                  "shotID": "SHOT_001",
                  "previewImage": "https://example.test/preview.png",
                  "categories": [ "LAND" ],
                  "subcategories": [ "YOSE" ],
                  "url-1080-H264": "https://example.test/video-1080.mp4",
                  "url-1080-SDR": "https://example.test/video-1080-hevc.mov"
                }
              ]
            }
            """;

        await using var tarStream = CreateTarArchive(json);
        var catalog = new AppleCatalog("tvos16", "tvOS 16", "https://example.test/catalog.tar");

        var assets = await AppleAerialProvider.ParseCatalogArchiveAsync(tarStream, catalog, CancellationToken.None);
        var asset = Assert.Single(assets);

        Assert.Equal("asset-1", asset.Id);
        Assert.Contains(asset.Variants, variant => variant.Name == "1080-hevc");
        Assert.Contains("landscapes", asset.Tags);
        Assert.Contains("yosemite", asset.Tags);
    }

    [Fact]
    public void MergeAssets_DeduplicatesByShotIdAndCombinesVariants()
    {
        var catalogA = new AppleCatalog("tvos16", "tvOS 16", "https://example.test/a.tar");
        var catalogB = new AppleCatalog("tvos13", "tvOS 13", "https://example.test/b.tar");

        var first = AppleAerialProvider.ParseEntriesJson(
            """
            {
              "assets": [
                {
                  "id": "asset-a",
                  "accessibilityLabel": "Monument Valley",
                  "shotID": "SHOT_DUP",
                  "url-1080-SDR": "https://example.test/shot-dup-1080.mov"
                }
              ]
            }
            """,
            catalogA);

        var second = AppleAerialProvider.ParseEntriesJson(
            """
            {
              "assets": [
                {
                  "id": "asset-b",
                  "accessibilityLabel": "Monument Valley",
                  "shotID": "SHOT_DUP",
                  "url-4K-SDR": "https://example.test/shot-dup-4k.mov"
                }
              ]
            }
            """,
            catalogB);

        var merged = AppleAerialProvider.MergeAssets(first, second);
        var asset = Assert.Single(merged);

        Assert.Contains(asset.Variants, variant => variant.Name == "1080-hevc");
        Assert.Contains(asset.Variants, variant => variant.Name == "4k-hevc");
    }

    private static MemoryStream CreateTarArchive(string entriesJson)
    {
        var stream = new MemoryStream();
        using (var writer = new TarWriter(stream, leaveOpen: true))
        {
            var dataStream = new MemoryStream(Encoding.UTF8.GetBytes(entriesJson));
            var entry = new PaxTarEntry(TarEntryType.RegularFile, "entries.json")
            {
                DataStream = dataStream,
            };

            writer.WriteEntry(entry);
        }

        stream.Position = 0;
        return stream;
    }
}
