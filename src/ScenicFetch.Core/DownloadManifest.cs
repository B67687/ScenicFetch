namespace ScenicFetch.Core;

public sealed record DownloadManifest(DateTimeOffset DownloadedAtUtc, IReadOnlyList<DownloadedAsset> Items);

public sealed record DownloadedAsset(
    string ItemId,
    string Source,
    string Title,
    MediaKind Kind,
    string FileName,
    string Url,
    string Variant,
    string? PageUrl,
    bool SkippedExisting,
    string? Warning,
    string? LicenseNote);
