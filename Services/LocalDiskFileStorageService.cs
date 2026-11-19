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

        var safeOriginalName = Path.GetFileName(file.FileName);
        var ext = Path.GetExtension(safeOriginalName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            return new FileUploadResult
            {
                Success = false,
                ErrorMessage = "Format foto harus .jpg, .jpeg, .png, atau .webp."
            };
        }

        // Inspect binary magic bytes to reject renamed non-image files
        byte[] header = new byte[12];
        await using (var probeStream = file.OpenReadStream())
        {
            int bytesRead = await probeStream.ReadAsync(header.AsMemory(0, header.Length), ct);
            if (!HasValidImageMagicBytes(ext, header, bytesRead))
            {
                return new FileUploadResult
                {
                    Success = false,
                    ErrorMessage = "Isi berkas tidak cocok dengan format gambar asli (JPEG/PNG/WebP)."
                };
            }
        }

        var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
            ? Path.Combine(_env.ContentRootPath, "wwwroot")
            : _env.WebRootPath;

        var uploadDir = Path.Combine(webRoot, "uploads", "products");
        Directory.CreateDirectory(uploadDir);

        var safeFileName = $"{Guid.NewGuid():N}{ext}";
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

    private static bool HasValidImageMagicBytes(string ext, byte[] header, int bytesRead)
    {
        return ext switch
        {
            ".jpg" or ".jpeg" => bytesRead >= 3 &&
                                 header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => bytesRead >= 8 &&
                      header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                      header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
            ".webp" => bytesRead >= 12 &&
                       header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                       header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
            _ => false
        };
    }
}
