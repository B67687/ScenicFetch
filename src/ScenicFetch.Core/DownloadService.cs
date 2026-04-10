using System.Text.Json;

namespace ScenicFetch.Core;

public sealed class DownloadService(HttpClient httpClient) : IDownloadService
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<DownloadManifest> DownloadAsync(
        IEnumerable<FetchItem> items,
        string outputDirectory,
        bool overwrite,
        string? preferredVariant,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        var downloadedItems = new List<DownloadedAsset>();

        foreach (var item in items)
        {
            var selectedVariant = SelectVariant(item, preferredVariant);
            var extension = InferExtension(selectedVariant.Url, item.Kind);
            var fileName =
                $"{item.Source.ToCliName()}_{Slugifier.Slugify(item.Id)}_{Slugifier.Slugify(selectedVariant.Name)}{extension}";
            var filePath = Path.Combine(outputDirectory, fileName);

            var skippedExisting = !overwrite && File.Exists(filePath);
            if (!skippedExisting)
            {
                using var response =
                    await _httpClient.GetAsync(
                        selectedVariant.Url,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using var responseStream =
                    await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var fileStream = File.Create(filePath);
                await responseStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            }

            downloadedItems.Add(
                new DownloadedAsset(
                    item.Id,
                    item.Source.ToCliName(),
                    item.Title,
                    item.Kind,
                    fileName,
                    selectedVariant.Url,
                    selectedVariant.Name,
                    item.PageUrl,
                    skippedExisting,
                    item.Warning,
                    item.LicenseNote));
        }

        var manifest = new DownloadManifest(DateTimeOffset.UtcNow, downloadedItems);
        var manifestPath = Path.Combine(outputDirectory, "manifest.json");
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(manifest, JsonDefaults.Serializer),
            cancellationToken).ConfigureAwait(false);

        return manifest;
    }

    private static VariantInfo SelectVariant(FetchItem item, string? preferredVariant)
    {
        if (!string.IsNullOrWhiteSpace(preferredVariant))
        {
            var explicitVariant = item.Variants.FirstOrDefault(
                variant => string.Equals(
                    variant.Name,
                    preferredVariant,
                    StringComparison.OrdinalIgnoreCase));
            if (explicitVariant is not null)
            {
                return explicitVariant;
            }
        }

        var primaryVariant = item.Variants.FirstOrDefault(
            variant => string.Equals(
                variant.Url,
                item.PrimaryUrl,
                StringComparison.OrdinalIgnoreCase));

        if (primaryVariant is not null)
        {
            return primaryVariant;
        }

        if (item.Variants.Count > 0)
        {
            return item.Variants[0];
        }

        return new VariantInfo("primary", item.PrimaryUrl);
    }

    private static string InferExtension(string url, MediaKind kind)
    {
        try
        {
            var uri = new Uri(url, UriKind.Absolute);
            var extension = Path.GetExtension(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                return extension;
            }
        }
        catch (UriFormatException)
        {
        }

        return kind == MediaKind.Video ? ".mov" : ".jpg";
    }
}
