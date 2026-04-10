using System.Text.Json;
using System.Text.Json.Serialization;
using ScenicFetch.Core;

namespace ScenicFetch.Providers;

public sealed class SpotlightProvider(HttpClient httpClient) : IFeedProvider
{
    private const string Endpoint = "https://fd.api.iris.microsoft.com/v4/api/selection";
    private const string PlacementId = "88000820";
    private readonly HttpClient _httpClient = httpClient;

    public SourceId Id => SourceId.Spotlight;

    public async Task<IReadOnlyList<FetchItem>> ListAsync(SourceQuery query, CancellationToken cancellationToken)
    {
        var country = query.GetOptionOrDefault("country", "US").ToUpperInvariant();
        var orientation = query.GetOptionOrDefault("orientation", "portrait").ToLowerInvariant();
        if (orientation is not ("portrait" or "landscape"))
        {
            throw new ArgumentException($"Unsupported Spotlight orientation '{orientation}'.", nameof(query));
        }

        var limit = query.LatestOnly ? 1 : Math.Clamp(query.Limit ?? 4, 1, 16);
        var requestUri =
            $"{Endpoint}?country={Uri.EscapeDataString(country)}&locale=en-{Uri.EscapeDataString(country)}&fmt=json&placement={PlacementId}&bcnt={limit}";

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<SpotlightPageResponse>(
            stream,
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? new SpotlightPageResponse();

        return MapItems(payload, orientation, country).Take(limit).ToArray();
    }

    internal static IReadOnlyList<FetchItem> MapItems(
        SpotlightPageResponse page,
        string orientation,
        string country)
    {
        var items = new List<FetchItem>();

        foreach (var batchItem in page.BatchResponse.Items)
        {
            if (string.IsNullOrWhiteSpace(batchItem.Item))
            {
                continue;
            }

            var nested = JsonSerializer.Deserialize<SpotlightPayload>(batchItem.Item);
            if (nested?.Ad is null)
            {
                continue;
            }

            var portrait = nested.Ad.PortraitImage?.Asset;
            var landscape = nested.Ad.LandscapeImage?.Asset;
            var selectedUrl = orientation == "portrait" ? portrait : landscape;
            if (string.IsNullOrWhiteSpace(selectedUrl))
            {
                selectedUrl = portrait ?? landscape;
            }

            if (string.IsNullOrWhiteSpace(selectedUrl))
            {
                continue;
            }

            items.Add(
                new FetchItem
                {
                    Id = BuildId(nested.Ad, selectedUrl),
                    Source = SourceId.Spotlight,
                    Kind = MediaKind.Image,
                    Title = string.IsNullOrWhiteSpace(nested.Ad.Title) ? "Windows Spotlight" : nested.Ad.Title,
                    Description = nested.Ad.Description,
                    Author = nested.Ad.Copyright,
                    PageUrl = SanitizeCtaUri(nested.Ad.CtaUri),
                    PrimaryUrl = selectedUrl,
                    PreviewUrl = selectedUrl,
                    Variants = BuildVariants(portrait, landscape),
                    Tags = ["spotlight", country],
                    LicenseNote = "Windows Spotlight imagery belongs to Microsoft or its respective rights holders.",
                    Warning = "Windows Spotlight uses an undocumented API and may break without notice.",
                });
        }

        return items;
    }

    private static IReadOnlyList<VariantInfo> BuildVariants(string? portrait, string? landscape)
    {
        var variants = new List<VariantInfo>();

        if (!string.IsNullOrWhiteSpace(portrait))
        {
            variants.Add(new VariantInfo("portrait", portrait));
        }

        if (!string.IsNullOrWhiteSpace(landscape))
        {
            variants.Add(new VariantInfo("landscape", landscape));
        }

        return variants;
    }

    private static string BuildId(SpotlightAd ad, string selectedUrl)
    {
        if (!string.IsNullOrWhiteSpace(ad.EntityId))
        {
            return ad.EntityId;
        }

        var uri = new Uri(selectedUrl, UriKind.Absolute);
        return Path.GetFileNameWithoutExtension(uri.AbsolutePath);
    }

    private static string? SanitizeCtaUri(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        var sanitized = uri.Replace("microsoft-edge:", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (sanitized.StartsWith("//", StringComparison.Ordinal))
        {
            return $"https:{sanitized}";
        }

        return sanitized;
    }
}

internal sealed record SpotlightPageResponse([property: JsonPropertyName("batchrsp")] SpotlightBatchResponse BatchResponse)
{
    public SpotlightPageResponse()
        : this(new SpotlightBatchResponse(Array.Empty<SpotlightBatchItem>()))
    {
    }
}

internal sealed record SpotlightBatchResponse([property: JsonPropertyName("items")] IReadOnlyList<SpotlightBatchItem> Items);

internal sealed record SpotlightBatchItem([property: JsonPropertyName("item")] string Item);

internal sealed record SpotlightPayload([property: JsonPropertyName("ad")] SpotlightAd Ad);

internal sealed record SpotlightAd(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("copyright")] string? Copyright,
    [property: JsonPropertyName("ctaUri")] string? CtaUri,
    [property: JsonPropertyName("entityId")] string? EntityId,
    [property: JsonPropertyName("portraitImage")] SpotlightImage? PortraitImage,
    [property: JsonPropertyName("landscapeImage")] SpotlightImage? LandscapeImage);

internal sealed record SpotlightImage([property: JsonPropertyName("asset")] string? Asset);
