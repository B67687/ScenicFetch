using System.Text.Json;
using ScenicFetch.Cli;
using ScenicFetch.Core;
using ScenicFetch.Providers;

namespace ScenicFetch.Tests;

public sealed class CliAppTests
{
    [Fact]
    public async Task SourcesList_AsJson_ReturnsThreeConfiguredSources()
    {
        var app = CreateApp(Array.Empty<FetchItem>());
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await app.RunAsync(["sources", "list", "--json"], stdout, stderr, CancellationToken.None);

        Assert.Equal(0, exitCode);

        using var document = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(3, document.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task ItemsList_AsJson_UsesProviderResults()
    {
        var app = CreateApp(
            [
                new FetchItem
                {
                    Id = "bing-1",
                    Source = SourceId.Bing,
                    Kind = MediaKind.Image,
                    Title = "Alps",
                    PrimaryUrl = "https://example.test/alps.jpg",
                    Variants = [new VariantInfo("UHD", "https://example.test/alps.jpg")],
                }
            ]);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await app.RunAsync(
            ["items", "list", "--source", "bing", "--json"],
            stdout,
            stderr,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("alps.jpg", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Download_WritesFileAndManifest()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "ScenicFetch.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var app = CreateApp(
                [
                    new FetchItem
                    {
                        Id = "bing-1",
                        Source = SourceId.Bing,
                        Kind = MediaKind.Image,
                        Title = "Forest",
                        PrimaryUrl = "https://example.test/forest.jpg",
                        Variants = [new VariantInfo("UHD", "https://example.test/forest.jpg")],
                    }
                ],
                new DownloadService(new HttpClient(new StaticResponseHandler("image-bytes"))));
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var exitCode = await app.RunAsync(
                ["download", "--source", "bing", "--output", tempDirectory],
                stdout,
                stderr,
                CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(Path.Combine(tempDirectory, "manifest.json")));
            Assert.Single(Directory.GetFiles(tempDirectory, "*.jpg"));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task UnknownOption_ReturnsNonZero()
    {
        var app = CreateApp(Array.Empty<FetchItem>());
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await app.RunAsync(
            ["items", "list", "--source", "bing", "--bogus"],
            stdout,
            stderr,
            CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains("Unknown option", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static CliApp CreateApp(IReadOnlyList<FetchItem> items, IDownloadService? downloadService = null)
    {
        var providers = new Dictionary<SourceId, IFeedProvider>
        {
            [SourceId.Bing] = new StaticFeedProvider(SourceId.Bing, items),
            [SourceId.Spotlight] = new StaticFeedProvider(SourceId.Spotlight, items),
            [SourceId.AppleAerial] = new StaticFeedProvider(SourceId.AppleAerial, items),
        };

        return new CliApp(
            ProviderCatalog.Descriptors,
            providers,
            downloadService ?? new DownloadService(new HttpClient(new StaticResponseHandler("bytes"))));
    }

    private sealed class StaticFeedProvider(SourceId id, IReadOnlyList<FetchItem> items) : IFeedProvider
    {
        public SourceId Id => id;

        public Task<IReadOnlyList<FetchItem>> ListAsync(SourceQuery query, CancellationToken cancellationToken)
        {
            _ = query;
            _ = cancellationToken;
            return Task.FromResult(items);
        }
    }

    private sealed class StaticResponseHandler(string body) : HttpMessageHandler
    {
        private readonly string _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;

            return Task.FromResult(
                new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(_body),
                });
        }
    }
}
