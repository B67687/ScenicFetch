using System.Text;
using System.Text.Json;
using ScenicFetch.Core;

namespace ScenicFetch.Cli;

public sealed class CliApp(
    IReadOnlyList<SourceDescriptor> descriptors,
    IReadOnlyDictionary<SourceId, IFeedProvider> providers,
    IDownloadService downloadService)
{
    private readonly IReadOnlyList<SourceDescriptor> _descriptors = descriptors;
    private readonly IReadOnlyDictionary<SourceId, IFeedProvider> _providers = providers;
    private readonly IDownloadService _downloadService = downloadService;

    public async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0 || IsHelpToken(args[0]))
        {
            await output.WriteLineAsync(BuildHelpText()).ConfigureAwait(false);
            return 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "sources" => await RunSourcesAsync(args.Skip(1).ToArray(), output, error, cancellationToken).ConfigureAwait(false),
            "items" => await RunItemsAsync(args.Skip(1).ToArray(), output, error, cancellationToken).ConfigureAwait(false),
            "download" => await RunDownloadAsync(args.Skip(1).ToArray(), output, error, cancellationToken).ConfigureAwait(false),
            _ => await WriteUsageErrorAsync(error, $"Unknown command '{args[0]}'.").ConfigureAwait(false),
        };
    }

    private async Task<int> RunSourcesAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (args.Length > 0 && !string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase))
        {
            return await WriteUsageErrorAsync(error, "Expected 'sources list'.").ConfigureAwait(false);
        }

        var optionArgs = args.Length > 0 && string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase)
            ? args.Skip(1).ToArray()
            : args;
        if (optionArgs.Any(item => !string.Equals(item, "--json", StringComparison.OrdinalIgnoreCase)))
        {
            return await WriteUsageErrorAsync(error, "Unknown option for 'sources list'.").ConfigureAwait(false);
        }

        var json = optionArgs.Any(item => string.Equals(item, "--json", StringComparison.OrdinalIgnoreCase));
        if (json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(_descriptors, JsonDefaults.Serializer)).ConfigureAwait(false);
            return 0;
        }

        foreach (var descriptor in _descriptors)
        {
            await output.WriteLineAsync(
                $"{descriptor.Id.ToCliName(),-14} {descriptor.DefaultKind,-5} {descriptor.Description}").ConfigureAwait(false);
        }

        return 0;
    }

    private async Task<int> RunItemsAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0 || !string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase))
        {
            return await WriteUsageErrorAsync(error, "Expected 'items list'.").ConfigureAwait(false);
        }

        if (!TryParseCommonOptions(args.Skip(1).ToArray(), requireOutput: false, out var options, out var parseError))
        {
            return await WriteUsageErrorAsync(error, parseError!).ConfigureAwait(false);
        }

        if (!TryResolveProvider(options.SourceId, out var provider))
        {
            return await WriteUsageErrorAsync(error, $"Unknown source '{options.SourceValue}'.").ConfigureAwait(false);
        }

        var query = BuildQuery(options, includeLatest: false);
        var items = await provider.ListAsync(query, cancellationToken).ConfigureAwait(false);
        if (options.Json)
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(items, JsonDefaults.Serializer)).ConfigureAwait(false);
            return 0;
        }

        foreach (var item in items)
        {
            var line = new StringBuilder();
            line.Append('[').Append(item.Source.ToCliName()).Append("] ");
            line.Append(item.Title);
            line.Append(" -> ");
            line.Append(item.PrimaryUrl);
            await output.WriteLineAsync(line.ToString()).ConfigureAwait(false);
        }

        return 0;
    }

    private async Task<int> RunDownloadAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryParseCommonOptions(args, requireOutput: true, out var options, out var parseError))
        {
            return await WriteUsageErrorAsync(error, parseError!).ConfigureAwait(false);
        }

        if (!TryResolveProvider(options.SourceId, out var provider))
        {
            return await WriteUsageErrorAsync(error, $"Unknown source '{options.SourceValue}'.").ConfigureAwait(false);
        }

        if (options.UseAll && options.UseLatest)
        {
            return await WriteUsageErrorAsync(error, "Use either --latest or --all, not both.").ConfigureAwait(false);
        }

        var query = BuildQuery(options, includeLatest: options.UseLatest);
        var items = await provider.ListAsync(query, cancellationToken).ConfigureAwait(false);
        if (items.Count == 0)
        {
            await error.WriteLineAsync("No items were returned for the requested source.").ConfigureAwait(false);
            return 1;
        }

        var manifest = await _downloadService.DownloadAsync(
            items,
            options.OutputDirectory!,
            options.Overwrite,
            options.OutputVariant,
            cancellationToken).ConfigureAwait(false);

        foreach (var item in manifest.Items)
        {
            var verb = item.SkippedExisting ? "existing" : "saved";
            await output.WriteLineAsync($"{verb}: {item.FileName}").ConfigureAwait(false);
        }

        await output.WriteLineAsync($"manifest: {Path.Combine(options.OutputDirectory!, "manifest.json")}").ConfigureAwait(false);
        return 0;
    }

    private static SourceQuery BuildQuery(ParsedOptions options, bool includeLatest)
    {
        var limit = includeLatest ? 1 : options.Limit;
        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        AddOption(dictionary, "resolution", options.Resolution);
        AddOption(dictionary, "country", options.Country);
        AddOption(dictionary, "orientation", options.Orientation);
        AddOption(dictionary, "catalog", options.Catalog);
        AddOption(dictionary, "variant", options.OutputVariant);

        return new SourceQuery
        {
            Limit = limit,
            LatestOnly = includeLatest,
            OutputVariant = options.OutputVariant,
            Options = dictionary,
        };
    }

    private static void AddOption(IDictionary<string, string> options, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            options[key] = value;
        }
    }

    private bool TryResolveProvider(SourceId? sourceId, out IFeedProvider provider)
    {
        if (sourceId is not null && _providers.TryGetValue(sourceId.Value, out provider!))
        {
            return true;
        }

        provider = null!;
        return false;
    }

    private static bool TryParseCommonOptions(
        IReadOnlyList<string> args,
        bool requireOutput,
        out ParsedOptions options,
        out string? error)
    {
        options = new ParsedOptions();
        error = null;

        if (args.Count > 0 && string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase))
        {
            args = args.Skip(1).ToArray();
        }

        for (var index = 0; index < args.Count; index++)
        {
            var current = args[index];
            switch (current)
            {
                case "--source":
                    if (!TryReadValue(args, ref index, out var sourceValue) ||
                        !SourceIdExtensions.TryParse(sourceValue, out var sourceId))
                    {
                        error = $"Unsupported source '{sourceValue ?? string.Empty}'.";
                        return false;
                    }

                    options = options with { SourceId = sourceId, SourceValue = sourceValue };
                    break;
                case "--limit":
                    if (!TryReadValue(args, ref index, out var limitValue) ||
                        !int.TryParse(limitValue, out var limit) ||
                        limit <= 0)
                    {
                        error = "Expected a positive integer after --limit.";
                        return false;
                    }

                    options = options with { Limit = limit };
                    break;
                case "--output":
                    if (!TryReadValue(args, ref index, out var outputDirectory) || string.IsNullOrWhiteSpace(outputDirectory))
                    {
                        error = "Expected a directory path after --output.";
                        return false;
                    }

                    options = options with { OutputDirectory = outputDirectory };
                    break;
                case "--json":
                    options = options with { Json = true };
                    break;
                case "--overwrite":
                    options = options with { Overwrite = true };
                    break;
                case "--latest":
                    options = options with { UseLatest = true };
                    break;
                case "--all":
                    options = options with { UseAll = true };
                    break;
                case "--resolution":
                    if (!TryReadValue(args, ref index, out var resolution))
                    {
                        error = "Expected a Bing resolution after --resolution.";
                        return false;
                    }

                    options = options with { Resolution = resolution };
                    break;
                case "--country":
                    if (!TryReadValue(args, ref index, out var country))
                    {
                        error = "Expected an ISO country code after --country.";
                        return false;
                    }

                    options = options with { Country = country };
                    break;
                case "--orientation":
                    if (!TryReadValue(args, ref index, out var orientation))
                    {
                        error = "Expected a Spotlight orientation after --orientation.";
                        return false;
                    }

                    options = options with { Orientation = orientation };
                    break;
                case "--catalog":
                    if (!TryReadValue(args, ref index, out var catalog))
                    {
                        error = "Expected an Apple catalog after --catalog.";
                        return false;
                    }

                    options = options with { Catalog = catalog };
                    break;
                case "--variant":
                    if (!TryReadValue(args, ref index, out var variant))
                    {
                        error = "Expected an Apple variant after --variant.";
                        return false;
                    }

                    options = options with { OutputVariant = variant };
                    break;
                default:
                    error = $"Unknown option '{current}'.";
                    return false;
            }
        }

        if (options.SourceId is null)
        {
            error = "The --source option is required.";
            return false;
        }

        if (requireOutput && string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            error = "The --output option is required for downloads.";
            return false;
        }

        return true;
    }

    private static bool TryReadValue(IReadOnlyList<string> args, ref int index, out string? value)
    {
        var nextIndex = index + 1;
        if (nextIndex >= args.Count || args[nextIndex].StartsWith("--", StringComparison.Ordinal))
        {
            value = null;
            return false;
        }

        value = args[nextIndex];
        index = nextIndex;
        return true;
    }

    private static bool IsHelpToken(string value) =>
        value is "--help" or "-h" or "help";

    private static async Task<int> WriteUsageErrorAsync(TextWriter error, string message)
    {
        await error.WriteLineAsync(message).ConfigureAwait(false);
        return 1;
    }

    private static string BuildHelpText() =>
        """
        ScenicFetch v1

        Commands:
          scenicfetch sources list [--json]
          scenicfetch items list --source <bing|spotlight|apple-aerial> [--limit N] [--json] [provider flags]
          scenicfetch download --source <bing|spotlight|apple-aerial> --output <dir> [--latest | --all] [--overwrite] [provider flags]

        Provider flags:
          Bing:      --resolution 1080x1920|768x1366|1366x768|1920x1080|UHD
          Spotlight: --country <ISO2> --orientation portrait|landscape
          Apple:     --catalog all|macos26|tvos16|tvos13 --variant 1080-hevc|4k-hevc|1080-h264
        """;

    private sealed record ParsedOptions
    {
        public SourceId? SourceId { get; init; }

        public string? SourceValue { get; init; }

        public int? Limit { get; init; }

        public bool Json { get; init; }

        public bool Overwrite { get; init; }

        public bool UseLatest { get; init; }

        public bool UseAll { get; init; }

        public string? OutputDirectory { get; init; }

        public string? Resolution { get; init; }

        public string? Country { get; init; }

        public string? Orientation { get; init; }

        public string? Catalog { get; init; }

        public string? OutputVariant { get; init; }
    }
}
