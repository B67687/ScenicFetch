namespace ScenicFetch.Core;

public sealed class SourceQuery
{
    public int? Limit { get; init; }

    public bool LatestOnly { get; init; }

    public string? OutputVariant { get; init; }

    public IReadOnlyDictionary<string, string> Options { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string? GetOption(string key)
    {
        if (Options.TryGetValue(key, out var value))
        {
            return value;
        }

        return null;
    }

    public string GetOptionOrDefault(string key, string fallback) =>
        string.IsNullOrWhiteSpace(GetOption(key)) ? fallback : GetOption(key)!;
}
