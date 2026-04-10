using ScenicFetch.Core;

namespace ScenicFetch.Providers;

public static class ProviderCatalog
{
    public static IReadOnlyList<SourceDescriptor> Descriptors { get; } =
        new[]
        {
            new SourceDescriptor(
                SourceId.Bing,
                "Bing",
                "Recent Bing homepage wallpapers as images.",
                MediaKind.Image,
                IsUndocumented: false),
            new SourceDescriptor(
                SourceId.Spotlight,
                "Spotlight",
                "Windows Spotlight imagery from Microsoft's undocumented content feed.",
                MediaKind.Image,
                IsUndocumented: true),
            new SourceDescriptor(
                SourceId.AppleAerial,
                "Apple Aerial",
                "Apple aerial video catalogs parsed from Apple-hosted manifest archives.",
                MediaKind.Video,
                IsUndocumented: true),
        };

    public static IReadOnlyDictionary<SourceId, IFeedProvider> CreateDefaultProviders(HttpClient httpClient) =>
        new Dictionary<SourceId, IFeedProvider>
        {
            [SourceId.Bing] = new BingProvider(httpClient),
            [SourceId.Spotlight] = new SpotlightProvider(httpClient),
            [SourceId.AppleAerial] = new AppleAerialProvider(httpClient),
        };
}
