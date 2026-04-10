using ScenicFetch.Core;
using ScenicFetch.Providers;

namespace ScenicFetch.Cli;

public static class ProgramEntry
{
    public static async Task<int> RunAsync(string[] args)
    {
        using var httpClient = ScenicFetchHttp.CreateDefaultClient();
        var providers = ProviderCatalog.CreateDefaultProviders(httpClient);
        var app = new CliApp(ProviderCatalog.Descriptors, providers, new DownloadService(httpClient));

        try
        {
            return await app.RunAsync(args, Console.Out, Console.Error, CancellationToken.None).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 1;
        }
        catch (HttpRequestException exception)
        {
            await Console.Error.WriteLineAsync($"Network request failed: {exception.Message}").ConfigureAwait(false);
            return 1;
        }
    }
}
