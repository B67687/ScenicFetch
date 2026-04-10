using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScenicFetch.Core;

namespace ScenicFetch.Providers;

public sealed class BingProvider(HttpClient httpClient) : IFeedProvider
{
    internal const string BaseUrl = "https://www.bing.com";
    internal const string DefaultResolution = "UHD";
    internal const string PreviewResolution = "768x1366";

    public static readonly IReadOnlyList<string> SupportedResolutions =
        ["1080x1920", "768x1366", "1366x768", "1920x1080", "UHD"];

    private readonly HttpClient _httpClient = httpClient;

    public SourceId Id => SourceId.Bing;

    public async Task<IReadOnlyList<FetchItem>> ListAsync(SourceQuery query, CancellationToken cancellationToken)
    {
        var resolution = query.GetOptionOrDefault("resolution", DefaultResolution);
        if (!SupportedResolutions.Any(item => string.Equals(item, resolution, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"Unsupported Bing resolution '{resolution}'.", nameof(query));
        }

        var limit = query.LatestOnly ? 1 : Math.Clamp(query.Limit ?? 7, 1, 7);

        using var response = await _httpClient.GetAsync(
            $"{BaseUrl}/HPImageArchive.aspx?format=js&n={limit}",
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<BingResponse>(
            contentStream,
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? new BingResponse();

        return MapImages(payload, resolution).Take(limit).ToArray();
    }

    internal static IReadOnlyList<FetchItem> MapImages(BingResponse response, string resolution)
    {
        return response.Images.Select(
            image =>
            {
                if (string.IsNullOrWhiteSpace(image.UrlBase))
                {
                    throw new InvalidOperationException("Bing response did not include a urlbase value.");
                }

                var urlBase = image.UrlBase;
                var title = string.IsNullOrWhiteSpace(image.Title) ? image.Copyright ?? "Bing image" : image.Title;
                var variants = SupportedResolutions
                    .Select(item => new VariantInfo(item, BuildImageUrl(urlBase, item)))
                    .ToArray();

                return new FetchItem
                {
                    Id = image.StartDate ?? urlBase,
                    Source = SourceId.Bing,
                    Kind = MediaKind.Image,
                    Title = title,
                    Author = image.Copyright,
                    PageUrl = BuildPageUrl(image.Quiz),
                    PrimaryUrl = BuildImageUrl(urlBase, resolution),
                    PreviewUrl = BuildImageUrl(urlBase, PreviewResolution),
                    Variants = variants,
                    Tags = ["bing", "homepage"],
                    CapturedAt = ParseDate(image.StartDate),
                    LicenseNote = "Bing homepage imagery belongs to its respective rights holders.",
                    Warning = "Bing's public image feed only exposes a small recent archive.",
                };
            }).ToArray();
    }

    internal static string BuildImageUrl(string urlBase, string resolution) => $"{BaseUrl}{urlBase}_{resolution}.jpg";

    private static string? BuildPageUrl(string? quiz)
    {
        if (string.IsNullOrWhiteSpace(quiz))
        {
            return null;
        }

        if (Uri.TryCreate(quiz, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        return $"{BaseUrl}{quiz}";
    }

    private static DateTimeOffset? ParseDate(string? startDate)
    {
        if (string.IsNullOrWhiteSpace(startDate))
        {
            return null;
        }

        return DateTimeOffset.TryParseExact(
            startDate,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
    }
}

internal sealed record BingResponse([property: JsonPropertyName("images")] IReadOnlyList<BingImage> Images)
{
    public BingResponse()
        : this(Array.Empty<BingImage>())
    {
    }
}

internal sealed record BingImage(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("copyright")] string? Copyright,
    [property: JsonPropertyName("quiz")] string? Quiz,
    [property: JsonPropertyName("startdate")] string? StartDate,
    [property: JsonPropertyName("urlbase")] string UrlBase);
