using System.ComponentModel.DataAnnotations;
using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaryaJayaFootwear.Pages.Admin;

public class ProductsModel : PageModel
{
    private readonly CatalogService _catalog;
    private readonly IFileStorageService _fileStorage;

    public ProductsModel(CatalogService catalog, IFileStorageService fileStorage)
    {
        _catalog = catalog;
        _fileStorage = fileStorage;
    }

    public List<Product> Products { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? HighlightProductId { get; set; }

    [BindProperty]
    public NewProductInput NewProduct { get; set; } = new();

    public sealed class NewProductInput
    {
        [Required, MaxLength(32)]
        public string Sku { get; set; } = string.Empty;

        [Required, MaxLength(140)]
        public string Name { get; set; } = string.Empty;

        public FootwearCategory Category { get; set; } = FootwearCategory.Sepatu;

        [Required, MaxLength(180)]
        public string ShortTagline { get; set; } = string.Empty;

        [Required, MaxLength(1500)]
        public string Description { get; set; } = string.Empty;

        [Required, MaxLength(160)]
        public string UpperMaterial { get; set; } = "Kanvas Katun 12oz";

        [Required, MaxLength(160)]
        public string SoleMaterial { get; set; } = "Karet Alam Vulkanisir";

        [Required, MaxLength(160)]
        public string ConstructionMethod { get; set; } = "Vulcanized Press Uap";

        [Range(10000, 10000000)]
        public decimal RetailPrice { get; set; } = 120000m;

        [Required]
        public string SvgIllustrationKey { get; set; } = "sepatu-kanvas";

        public bool IsFeatured { get; set; }

        [Required, MaxLength(60)]
        public string DefaultColorName { get; set; } = "Cokelat Pelana";

        [Required, MaxLength(12)]
        public string DefaultColorHex { get; set; } = "#4A2E1B";

        [Range(0, 10000)]
        public int InitialStockPerSize { get; set; } = 25;

        public IFormFile? PhotoFile { get; set; }
    }

    public async Task OnGetAsync()
    {
        Products = await _catalog.GetAllProductsForAdminAsync();
    }

    public async Task<IActionResult> OnPostCreateProductAsync()
    {
        if (!ModelState.IsValid)
        {
            Products = await _catalog.GetAllProductsForAdminAsync();
            TempData["ToastError"] = "Periksa kembali isian produk baru Anda.";
            return Page();
        }

        try
        {
            string? imageUrl = null;
            if (NewProduct.PhotoFile is not null && NewProduct.PhotoFile.Length > 0)
            {
                var upload = await _fileStorage.SaveProductImageAsync(NewProduct.PhotoFile);
                if (!upload.Success)
                {
                    Products = await _catalog.GetAllProductsForAdminAsync();
                    TempData["ToastError"] = upload.ErrorMessage ?? "Gagal mengunggah foto produk.";
                    return Page();
                }
                imageUrl = upload.RelativeUrl;
            }

            var created = await _catalog.CreateProductAsync(
                NewProduct.Sku,
                NewProduct.Name,
                NewProduct.Category,
                NewProduct.ShortTagline,
                NewProduct.Description,
                NewProduct.UpperMaterial,
                NewProduct.SoleMaterial,
                NewProduct.ConstructionMethod,
                NewProduct.RetailPrice,
                NewProduct.SvgIllustrationKey,
                imageUrl,
                NewProduct.IsFeatured,
                NewProduct.DefaultColorName,
                NewProduct.DefaultColorHex,
                NewProduct.InitialStockPerSize);

            TempData["ToastSuccess"] = $"Produk '{created.Name}' ({created.Sku}) berhasil ditambahkan beserta varian ukuran 38–44.";
            return RedirectToPage(new { HighlightProductId = created.Id });
        }
        catch (Exception)
        {
            Products = await _catalog.GetAllProductsForAdminAsync();
            TempData["ToastError"] = "Kode SKU sudah digunakan produk lain atau terjadi kesalahan penyimpanan.";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostUpdateProductAsync(
        int productId,
        string name,
        FootwearCategory category,
        string shortTagline,
        string description,
        string upperMaterial,
        string soleMaterial,
        string constructionMethod,
        decimal retailPrice,
        string svgIllustrationKey,
        bool isFeatured,
        bool isActive,
        IFormFile? photoFile)
    {
        string? newImageUrl = null;
        if (photoFile is not null && photoFile.Length > 0)
        {
            var upload = await _fileStorage.SaveProductImageAsync(photoFile);
            if (!upload.Success)
            {
                TempData["ToastError"] = upload.ErrorMessage ?? "Gagal mengunggah foto produk.";
                return RedirectToPage(new { HighlightProductId = productId });
            }
            newImageUrl = upload.RelativeUrl;
        }

        var ok = await _catalog.UpdateProductAsync(
            productId, name, category, shortTagline, description,
            upperMaterial, soleMaterial, constructionMethod,
            retailPrice, svgIllustrationKey, newImageUrl, isFeatured, isActive);

        TempData[ok ? "ToastSuccess" : "ToastError"] = ok
            ? "Data produk berhasil diperbarui."
            : "Produk tidak ditemukan.";

        return RedirectToPage(new { HighlightProductId = productId });
    }

    public async Task<IActionResult> OnPostUpdateStockAsync(int productId, int variantId, int stockQuantity)
    {
        var ok = await _catalog.UpdateVariantStockAsync(variantId, stockQuantity);
        TempData[ok ? "ToastSuccess" : "ToastError"] = ok
            ? "Stok varian gudang diperbarui."
            : "Varian tidak ditemukan.";

        return RedirectToPage(new { HighlightProductId = productId });
    }

    public async Task<IActionResult> OnPostAddColorwayAsync(int productId, string colorName, string colorHex, int stockPerSize)
    {
        var ok = await _catalog.AddColorwayVariantsAsync(productId, colorName, colorHex, stockPerSize);
        TempData[ok ? "ToastSuccess" : "ToastError"] = ok
            ? $"Warna baru '{colorName}' (Uk. 38–44) ditambahkan."
            : "Gagal menambahkan varian warna baru.";

        return RedirectToPage(new { HighlightProductId = productId });
    }

    public async Task<IActionResult> OnPostDeleteProductAsync(int productId)
    {
        var ok = await _catalog.DeleteProductAsync(productId);
        TempData[ok ? "ToastInfo" : "ToastError"] = ok
            ? "Produk dihapus atau dinonaktifkan dari katalog."
            : "Produk tidak ditemukan.";

        return RedirectToPage();
    }
}
