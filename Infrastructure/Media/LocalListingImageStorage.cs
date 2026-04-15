using Microsoft.Extensions.Options;

namespace CarHub.Api.Infrastructure.Media;

public sealed class LocalListingImageStorage(
    IWebHostEnvironment environment,
    IOptions<MediaOptions> options) : IListingImageStorage
{
    private readonly MediaOptions _options = options.Value;

    public async Task<StoredFileResult> SaveAsync(Guid listingId, IFormFile file, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (!_options.AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unsupported file type.");
        }

        var maxBytes = _options.MaxImageSizeMb * 1024 * 1024;
        if (file.Length <= 0 || file.Length > maxBytes)
        {
            throw new InvalidOperationException($"Image size must be between 1 byte and { _options.MaxImageSizeMb } MB.");
        }

        var baseRoot = Path.IsPathRooted(_options.UploadsRoot)
            ? _options.UploadsRoot
            : Path.Combine(environment.ContentRootPath, _options.UploadsRoot);

        var listingFolder = Path.Combine(baseRoot, "listings", listingId.ToString("N"));
        Directory.CreateDirectory(listingFolder);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(listingFolder, fileName);

        await using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(stream, cancellationToken);

        var storageKey = Path.Combine("uploads", "listings", listingId.ToString("N"), fileName).Replace('\\', '/');
        var url = "/" + storageKey;

        return new StoredFileResult(storageKey, url);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return Task.CompletedTask;
        }

        var sanitized = storageKey.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(environment.ContentRootPath, "wwwroot", sanitized);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }
}
