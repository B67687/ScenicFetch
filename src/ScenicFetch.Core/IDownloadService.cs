namespace ScenicFetch.Core;

public interface IDownloadService
{
    Task<DownloadManifest> DownloadAsync(
        IEnumerable<FetchItem> items,
        string outputDirectory,
        bool overwrite,
        string? preferredVariant,
        CancellationToken cancellationToken);
}
