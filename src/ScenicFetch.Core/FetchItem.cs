namespace ScenicFetch.Core;

public sealed record FetchItem
{
    public required string Id { get; init; }

    public required SourceId Source { get; init; }

    public required MediaKind Kind { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? Author { get; init; }

    public string? PageUrl { get; init; }

    public required string PrimaryUrl { get; init; }

    public string? PreviewUrl { get; init; }

    public IReadOnlyList<VariantInfo> Variants { get; init; } = Array.Empty<VariantInfo>();

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public DateTimeOffset? CapturedAt { get; init; }

    public string? LicenseNote { get; init; }

    public string? Warning { get; init; }
}
