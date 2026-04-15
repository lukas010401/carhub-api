namespace CarHub.Api.Infrastructure.Media;

public sealed record StoredFileResult(string StorageKey, string Url);

public interface IListingImageStorage
{
    Task<StoredFileResult> SaveAsync(Guid listingId, IFormFile file, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
