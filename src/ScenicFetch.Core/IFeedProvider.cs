namespace ScenicFetch.Core;

public interface IFeedProvider
{
    SourceId Id { get; }

    Task<IReadOnlyList<FetchItem>> ListAsync(SourceQuery query, CancellationToken cancellationToken);
}
