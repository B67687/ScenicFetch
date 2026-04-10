namespace ScenicFetch.Core;

public sealed record SourceDescriptor(
    SourceId Id,
    string Name,
    string Description,
    MediaKind DefaultKind,
    bool IsUndocumented);
