using System.Formats.Tar;
using System.Text.Json;
using ScenicFetch.Core;

namespace ScenicFetch.Providers;

public sealed class AppleAerialProvider(HttpClient httpClient) : IFeedProvider
{
    private const string DefaultCatalog = "all";
    internal const string DefaultVariant = "1080-hevc";
    internal const string Warning =
        "Apple Aerial feeds are undocumented, and ScenicFetch only bypasses TLS validation for sylvan.apple.com.";
    internal const string LicenseNote =
        "Apple Aerial media remains subject to Apple's terms and the rights of the underlying content owners.";

    private static readonly AppleCatalog[] Catalogs =
    [
        new(
            "macos26",
            "macOS 26",
            "https://sylvan.apple.com/itunes-assets/Aerials126/v4/82/2e/34/822e344c-f5d2-878c-3d56-508d5b09ed61/resources-26-0-1.tar"),
        new(
            "tvos16",
            "tvOS 16",
            "https://sylvan.apple.com/Aerials/resources-16.tar"),
        new(
            "tvos13",
            "tvOS 13",
            "https://sylvan.apple.com/Aerials/resources-13.tar"),
    ];

    private readonly HttpClient _httpClient = httpClient;

    public SourceId Id => SourceId.AppleAerial;

    public async Task<IReadOnlyList<FetchItem>> ListAsync(SourceQuery query, CancellationToken cancellationToken)
    {
        var catalogKey = query.GetOptionOrDefault("catalog", DefaultCatalog).ToLowerInvariant();
        var selectedVariant = NormalizeVariant(query.OutputVariant ?? query.GetOption("variant") ?? DefaultVariant);
        var selectedCatalogs = ResolveCatalogs(catalogKey);
        var assets = new List<AppleAsset>();

        foreach (var catalog in selectedCatalogs)
        {
            await using var stream = await _httpClient.GetStreamAsync(catalog.ManifestUri, cancellationToken).ConfigureAwait(false);
            var parsedAssets = await ParseCatalogArchiveAsync(stream, catalog, cancellationToken).ConfigureAwait(false);
            MergeInto(assets, parsedAssets);
        }

        var items = assets
            .Select(asset => asset.ToFetchItem(selectedVariant))
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (query.LatestOnly && items.Count > 1)
        {
            items = items.Take(1).ToList();
        }

        if (query.Limit is int limit and > 0)
        {
            items = items.Take(limit).ToList();
        }

        return items;
    }

    internal static IReadOnlyList<AppleAsset> ParseEntriesJson(string json, AppleCatalog catalog)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var categoryMap = BuildCategoryMap(root);

        if (!root.TryGetProperty("assets", out var assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<AppleAsset>();
        }

        var results = new List<AppleAsset>();

        foreach (var assetElement in assetsElement.EnumerateArray())
        {
            var asset = TryCreateAsset(assetElement, categoryMap, catalog);
            if (asset is not null)
            {
                results.Add(asset);
            }
        }

        return results;
    }

    internal static IReadOnlyList<AppleAsset> MergeAssets(params IEnumerable<AppleAsset>[] collections)
    {
        var merged = new List<AppleAsset>();

        foreach (var collection in collections)
        {
            MergeInto(merged, collection);
        }

        return merged;
    }

    private static IEnumerable<AppleCatalog> ResolveCatalogs(string catalogKey)
    {
        if (catalogKey == DefaultCatalog)
        {
            return Catalogs;
        }

        var selectedCatalog = Catalogs.FirstOrDefault(
            catalog => string.Equals(catalog.Key, catalogKey, StringComparison.OrdinalIgnoreCase));
        if (selectedCatalog is null)
        {
            throw new ArgumentException($"Unsupported Apple Aerial catalog '{catalogKey}'.", nameof(catalogKey));
        }

        return [selectedCatalog];
    }

    private static string NormalizeVariant(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "1080-h264" => "1080-h264",
            "4k-hevc" => "4k-hevc",
            _ => "1080-hevc",
        };

    private static void MergeInto(List<AppleAsset> existingAssets, IEnumerable<AppleAsset> incomingAssets)
    {
        foreach (var incomingAsset in incomingAssets)
        {
            var existingAsset = existingAssets.FirstOrDefault(candidate => candidate.Matches(incomingAsset));
            if (existingAsset is null)
            {
                existingAssets.Add(incomingAsset);
            }
            else
            {
                existingAsset.MergeFrom(incomingAsset);
            }
        }
    }

    internal static async Task<IReadOnlyList<AppleAsset>> ParseCatalogArchiveAsync(
        Stream tarStream,
        AppleCatalog catalog,
        CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await tarStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;

        using var reader = new TarReader(memoryStream);
        TarEntry? entry;

        while ((entry = reader.GetNextEntry()) is not null)
        {
            var entryName = entry.Name.TrimStart('.', '/');
            if (!string.Equals(entryName, "entries.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.DataStream is null)
            {
                break;
            }

            using var streamReader = new StreamReader(entry.DataStream, leaveOpen: true);
            var json = await streamReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return ParseEntriesJson(json, catalog);
        }

        return Array.Empty<AppleAsset>();
    }

    private static Dictionary<string, string> BuildCategoryMap(JsonElement root)
    {
        var categories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!root.TryGetProperty("categories", out var categoriesElement) || categoriesElement.ValueKind != JsonValueKind.Array)
        {
            return categories;
        }

        foreach (var category in categoriesElement.EnumerateArray())
        {
            AddCategory(categories, category);

            if (!category.TryGetProperty("subcategories", out var subcategoriesElement) ||
                subcategoriesElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var subcategory in subcategoriesElement.EnumerateArray())
            {
                AddCategory(categories, subcategory);
            }
        }

        return categories;
    }

    private static void AddCategory(Dictionary<string, string> categories, JsonElement element)
    {
        var id = GetString(element, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var candidate =
            ToFriendlyName(GetString(element, "localizedNameKey")) ??
            ToFriendlyName(GetString(element, "localizedDescriptionKey")) ??
            GetString(element, "accessibilityLabel") ??
            id;
        categories[id] = candidate;
    }

    private static AppleAsset? TryCreateAsset(
        JsonElement assetElement,
        IReadOnlyDictionary<string, string> categoryMap,
        AppleCatalog catalog)
    {
        var id = GetString(assetElement, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var title =
            GetString(assetElement, "accessibilityLabel") ??
            ToFriendlyName(GetString(assetElement, "localizedNameKey")) ??
            GetString(assetElement, "shotID") ??
            id;

        var variants = new List<VariantInfo>();
        AddVariant(variants, "1080-h264", GetString(assetElement, "url-1080-H264"));
        AddVariant(variants, "1080-hevc", GetString(assetElement, "url-1080-SDR"));
        AddVariant(variants, "4k-hevc", GetString(assetElement, "url-4K-SDR"));
        AddVariant(variants, "1080-hdr", GetString(assetElement, "url-1080-HDR"));
        AddVariant(variants, "4k-hdr", GetString(assetElement, "url-4K-HDR"));
        AddVariantIfMissing(variants, "4k-hevc", GetString(assetElement, "url-4K-SDR-240FPS"), "240fps");
        AddVariantIfMissing(variants, "4k-hevc", GetString(assetElement, "url-4K-SDR-120FPS"), "120fps");

        if (variants.Count == 0)
        {
            return null;
        }

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            catalog.Key,
            catalog.DisplayName.ToLowerInvariant(),
        };

        CollectTags(assetElement, "categories", categoryMap, tags);
        CollectTags(assetElement, "subcategories", categoryMap, tags);

        return new AppleAsset(
            id,
            title,
            GetString(assetElement, "shotID"),
            GetString(assetElement, "previewImage"),
            GetString(assetElement, "localizedNameKey"),
            variants,
            tags);
    }

    private static void CollectTags(
        JsonElement element,
        string propertyName,
        IReadOnlyDictionary<string, string> categoryMap,
        ISet<string> tags)
    {
        if (!element.TryGetProperty(propertyName, out var valuesElement) || valuesElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var value in valuesElement.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var key = value.GetString();
            if (!string.IsNullOrWhiteSpace(key) && categoryMap.TryGetValue(key, out var categoryName))
            {
                tags.Add(categoryName);
            }
        }
    }

    private static string? ToFriendlyName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value
            .Replace("AerialCategory", string.Empty, StringComparison.Ordinal)
            .Replace("AerialSubcategory", string.Empty, StringComparison.Ordinal)
            .Replace("Description", string.Empty, StringComparison.Ordinal)
            .Replace("_NAME", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (cleaned.Length == 0)
        {
            return null;
        }

        var builder = new System.Text.StringBuilder(cleaned.Length * 2);

        for (var index = 0; index < cleaned.Length; index++)
        {
            var character = cleaned[index];
            if (index > 0 && char.IsUpper(character) && char.IsLower(cleaned[index - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(character);
        }

        var result = builder.ToString().Replace('_', ' ').Trim();
        return result.Length == 0 ? null : result.ToLowerInvariant();
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return null;
    }

    private static void AddVariant(List<VariantInfo> variants, string name, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        variants.Add(new VariantInfo(name, url));
    }

    private static void AddVariantIfMissing(List<VariantInfo> variants, string name, string? url, string note)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            variants.Any(variant => string.Equals(variant.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        variants.Add(new VariantInfo(name, url, note));
    }
}

internal sealed record AppleCatalog(string Key, string DisplayName, string ManifestUri);

internal sealed class AppleAsset(
    string id,
    string title,
    string? shotId,
    string? previewUrl,
    string? localizedNameKey,
    IEnumerable<VariantInfo> variants,
    IEnumerable<string> tags)
{
    private readonly List<VariantInfo> _variants = variants.ToList();
    private readonly HashSet<string> _tags = new(tags, StringComparer.OrdinalIgnoreCase);

    public string Id { get; } = id;

    public string Title { get; private set; } = title;

    public string? ShotId { get; } = shotId;

    public string? PreviewUrl { get; private set; } = previewUrl;

    public string? LocalizedNameKey { get; } = localizedNameKey;

    public IReadOnlyList<VariantInfo> Variants => _variants;

    public IReadOnlyCollection<string> Tags => _tags;

    public bool Matches(AppleAsset other)
    {
        if (string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(ShotId) &&
            !string.IsNullOrWhiteSpace(other.ShotId) &&
            string.Equals(ShotId, other.ShotId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var thisFileNames = new HashSet<string>(
            _variants.Select(variant => Path.GetFileName(new Uri(variant.Url).AbsolutePath)),
            StringComparer.OrdinalIgnoreCase);

        return other._variants.Any(
            variant => thisFileNames.Contains(Path.GetFileName(new Uri(variant.Url).AbsolutePath)));
    }

    public void MergeFrom(AppleAsset other)
    {
        foreach (var variant in other._variants)
        {
            if (_variants.Any(existing => string.Equals(existing.Name, variant.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _variants.Add(variant);
        }

        foreach (var tag in other._tags)
        {
            _tags.Add(tag);
        }

        if (string.IsNullOrWhiteSpace(PreviewUrl))
        {
            PreviewUrl = other.PreviewUrl;
        }

        if (Title.Length < other.Title.Length)
        {
            Title = other.Title;
        }
    }

    public FetchItem ToFetchItem(string preferredVariant)
    {
        var primaryVariant =
            _variants.FirstOrDefault(
                variant => string.Equals(variant.Name, preferredVariant, StringComparison.OrdinalIgnoreCase)) ??
            _variants.FirstOrDefault(
                variant => string.Equals(variant.Name, AppleAerialProvider.DefaultVariant, StringComparison.OrdinalIgnoreCase)) ??
            _variants.FirstOrDefault(
                variant => string.Equals(variant.Name, "4k-hevc", StringComparison.OrdinalIgnoreCase)) ??
            _variants.First();

        return new FetchItem
        {
            Id = Id,
            Source = SourceId.AppleAerial,
            Kind = MediaKind.Video,
            Title = Title,
            Description = string.IsNullOrWhiteSpace(ShotId) ? LocalizedNameKey : ShotId,
            PrimaryUrl = primaryVariant.Url,
            PreviewUrl = PreviewUrl,
            Variants = _variants
                .OrderBy(variant => GetVariantPriority(variant.Name))
                .ThenBy(variant => variant.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Tags = _tags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ToArray(),
            LicenseNote = AppleAerialProvider.LicenseNote,
            Warning = AppleAerialProvider.Warning,
        };
    }

    private static int GetVariantPriority(string name) =>
        name.ToLowerInvariant() switch
        {
            "1080-hevc" => 0,
            "4k-hevc" => 1,
            "1080-h264" => 2,
            _ => 3,
        };
}
