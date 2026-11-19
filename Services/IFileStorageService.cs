using Microsoft.AspNetCore.Http;

namespace KaryaJayaFootwear.Services;

public sealed class FileUploadResult
{
    public bool Success { get; init; }
    public string? RelativeUrl { get; init; }
    public string? ErrorMessage { get; init; }
}

public interface IFileStorageService
{
    Task<FileUploadResult> SaveProductImageAsync(IFormFile file, CancellationToken ct = default);
    void DeleteProductImageIfLocal(string? imageUrl);
}
