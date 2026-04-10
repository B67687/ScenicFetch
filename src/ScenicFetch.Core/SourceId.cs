using System.Text.Json.Serialization;

namespace ScenicFetch.Core;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SourceId
{
    Bing,
    Spotlight,
    AppleAerial,
}

public static class SourceIdExtensions
{
    public static string ToCliName(this SourceId sourceId) =>
        sourceId switch
        {
            SourceId.Bing => "bing",
            SourceId.Spotlight => "spotlight",
            SourceId.AppleAerial => "apple-aerial",
            _ => throw new ArgumentOutOfRangeException(nameof(sourceId), sourceId, "Unknown source."),
        };

    public static bool TryParse(string? value, out SourceId sourceId)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "bing":
                sourceId = SourceId.Bing;
                return true;
            case "spotlight":
                sourceId = SourceId.Spotlight;
                return true;
            case "apple-aerial":
                sourceId = SourceId.AppleAerial;
                return true;
            default:
                sourceId = default;
                return false;
        }
    }
}
