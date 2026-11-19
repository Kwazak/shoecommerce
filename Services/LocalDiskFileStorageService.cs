using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace KaryaJayaFootwear.Services;

public sealed class LocalDiskFileStorageService : IFileStorageService
{
    private const long MaxBytes = 3 * 1024 * 1024; // 3 MB
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly IWebHostEnvironment _env;

    public LocalDiskFileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<FileUploadResult> SaveProductImageAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length <= 0)
        {
            return new FileUploadResult { Success = false, ErrorMessage = "Berkas foto kosong." };
        }

        if (file.Length > MaxBytes)
        {
            return new FileUploadResult
            {
                Success = false,
                ErrorMessage = "Ukuran foto maksimal 3 MB."
            };
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            return new FileUploadResult
            {
                Success = false,
                ErrorMessage = "Format foto harus .jpg, .jpeg, .png, atau .webp."
            };
        }

        var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
            ? Path.Combine(_env.ContentRootPath, "wwwroot")
            : _env.WebRootPath;

        var uploadDir = Path.Combine(webRoot, "uploads", "products");
        Directory.CreateDirectory(uploadDir);

        var safeFileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploadDir, safeFileName);

        await using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, ct);
        }

        return new FileUploadResult
        {
            Success = true,
            RelativeUrl = $"/uploads/products/{safeFileName}"
        };
    }

    public void DeleteProductImageIfLocal(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("/uploads/products/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fileName = Path.GetFileName(imageUrl);
        if (string.IsNullOrWhiteSpace(fileName)) return;

        var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
            ? Path.Combine(_env.ContentRootPath, "wwwroot")
            : _env.WebRootPath;

        var fullPath = Path.Combine(webRoot, "uploads", "products", fileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
