using KaryaJayaFootwear.Data;
using KaryaJayaFootwear.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace KaryaJayaFootwear.Services;

public sealed class CatalogService
{
    private readonly AppDbContext _db;
    private readonly PricingEngineService _pricing;
    private readonly HybridCache _cache;

    public CatalogService(AppDbContext db, PricingEngineService pricing, HybridCache cache)
    {
        _db = db;
        _pricing = pricing;
        _cache = cache;
    }

    public sealed class CachedProductSnapshot
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public FootwearCategory Category { get; set; }
        public string ShortTagline { get; set; } = string.Empty;
        public string UpperMaterial { get; set; } = string.Empty;
        public string SoleMaterial { get; set; } = string.Empty;
        public decimal RetailPrice { get; set; }
        public string SvgIllustrationKey { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public bool IsFeatured { get; set; }
        public List<CachedVariantSnapshot> Variants { get; set; } = new();
    }

    public sealed class CachedVariantSnapshot
    {
        public int Id { get; set; }
        public int Size { get; set; }
        public string ColorHex { get; set; } = "#4A2E1B";
        public int AvailableStock { get; set; }
    }

    private async Task<List<CachedProductSnapshot>> GetCachedActiveProductsAsync()
    {
        return await _cache.GetOrCreateAsync(
            "catalog:active-products",
            async cancel => await _db.Products
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.IsFeatured)
                .ThenBy(p => p.Category)
                .ThenBy(p => p.Name)
                .Select(p => new CachedProductSnapshot
                {
                    Id = p.Id,
                    Sku = p.Sku,
                    Name = p.Name,
                    Category = p.Category,
                    ShortTagline = p.ShortTagline,
                    UpperMaterial = p.UpperMaterial,
                    SoleMaterial = p.SoleMaterial,
                    RetailPrice = p.RetailPrice,
                    SvgIllustrationKey = p.SvgIllustrationKey,
                    ImageUrl = p.ImageUrl,
                    IsFeatured = p.IsFeatured,
                    Variants = p.Variants.Select(v => new CachedVariantSnapshot
                    {
                        Id = v.Id,
                        Size = v.Size,
                        ColorHex = v.ColorHex,
                        AvailableStock = v.PhysicalStock - v.ReservedStock
                    }).ToList()
                })
                .ToListAsync(cancel),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(20),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            },
            tags: new[] { "catalog" });
    }

    public async Task<List<StarterBundle>> GetStarterBundlesAsync()
    {
        return await _cache.GetOrCreateAsync(
            "catalog:starter-bundles",
            async cancel => await _db.StarterBundles
                .AsNoTracking()
                .Where(b => b.IsActive)
                .OrderBy(b => b.TotalPairs)
                .ThenBy(b => b.BundlePrice)
                .ToListAsync(cancel),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(30),
                LocalCacheExpiration = TimeSpan.FromMinutes(15)
            },
            tags: new[] { "catalog" });
    }

    public async Task<List<ProductCatalogCardViewModel>> GetCatalogAsync(CatalogFilterQuery filter, bool isApprovedB2B)
    {
        var tiers = await _pricing.GetActiveTiersAsync();
        bool showWholesale = isApprovedB2B || filter.SimulateB2B;
        int targetPairs = filter.SimulateB2B ? Math.Max(6, filter.SimulationPairs) : (tiers.FirstOrDefault()?.MinPairs ?? 6);
        var previewTier = showWholesale ? _pricing.ResolveTierForPairs(tiers, targetPairs) : null;
        decimal discountPct = previewTier?.DiscountPercent ?? 0m;

        IEnumerable<CachedProductSnapshot> query = await GetCachedActiveProductsAsync();

        if (filter.Category.HasValue)
        {
            query = query.Where(p => p.Category == filter.Category.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Sku.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.ShortTagline.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.UpperMaterial.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.MinPrice.HasValue)
        {
            query = query.Where(p => p.RetailPrice >= filter.MinPrice.Value);
        }

        if (filter.MaxPrice.HasValue)
        {
            query = query.Where(p => p.RetailPrice <= filter.MaxPrice.Value);
        }

        if (filter.Size.HasValue)
        {
            var sizeVal = filter.Size.Value;
            query = query.Where(p => p.Variants.Any(v => v.Size == sizeVal && (!filter.InStockOnly || v.AvailableStock > 0)));
        }
        else if (filter.InStockOnly)
        {
            query = query.Where(p => p.Variants.Any(v => v.AvailableStock > 0));
        }

        return query.Select(p =>
        {
            var totalStock = p.Variants.Sum(v => Math.Max(0, v.AvailableStock));
            var availableSizes = p.Variants
                .Where(v => v.AvailableStock > 0)
                .Select(v => v.Size)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            var firstColor = p.Variants.FirstOrDefault()?.ColorHex ?? "#4A2E1B";

            return new ProductCatalogCardViewModel
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                Category = p.Category,
                ShortTagline = p.ShortTagline,
                UpperMaterial = p.UpperMaterial,
                SoleMaterial = p.SoleMaterial,
                RetailPrice = p.RetailPrice,
                DisplayWholesalePrice = discountPct > 0
                    ? _pricing.CalculateDiscountedUnitPrice(p.RetailPrice, discountPct)
                    : p.RetailPrice,
                DisplayDiscountPercent = discountPct,
                SvgIllustrationKey = p.SvgIllustrationKey,
                ImageUrl = p.ImageUrl,
                PrimaryColorHex = firstColor,
                TotalStock = totalStock,
                AvailableSizes = availableSizes,
                IsFeatured = p.IsFeatured
            };
        }).ToList();
    }

    public async Task<List<ProductCatalogCardViewModel>> GetFeaturedProductsAsync(bool isApprovedB2B, int take = 6)
    {
        var all = await GetCatalogAsync(new CatalogFilterQuery(), isApprovedB2B);
        return all.Where(p => p.IsFeatured).Take(take).ToList();
    }

    public async Task<Dictionary<FootwearCategory, int>> GetCategoryCountsAsync()
    {
        var cached = await GetCachedActiveProductsAsync();
        var counts = cached
            .GroupBy(p => p.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (FootwearCategory cat in Enum.GetValues<FootwearCategory>())
        {
            counts.TryAdd(cat, 0);
        }

        return counts;
    }

    public async Task<Product?> GetProductDetailsAsync(int id)
    {
        return await _db.Products
            .AsNoTracking()
            .Include(p => p.Variants.OrderBy(v => v.ColorName).ThenBy(v => v.Size))
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Product>> GetAllProductsForAdminAsync()
    {
        return await _db.Products
            .AsNoTracking()
            .Include(p => p.Variants.OrderBy(v => v.ColorName).ThenBy(v => v.Size))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Product> CreateProductAsync(
        string sku,
        string name,
        FootwearCategory category,
        string shortTagline,
        string description,
        string upperMaterial,
        string soleMaterial,
        string constructionMethod,
        decimal retailPrice,
        string svgIllustrationKey,
        string? imageUrl,
        bool isFeatured,
        string defaultColorName,
        string defaultColorHex,
        int initialStockPerSize)
    {
        var product = new Product
        {
            Sku = sku.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Category = category,
            ShortTagline = shortTagline.Trim(),
            Description = description.Trim(),
            UpperMaterial = upperMaterial.Trim(),
            SoleMaterial = soleMaterial.Trim(),
            ConstructionMethod = constructionMethod.Trim(),
            RetailPrice = Math.Max(1000m, retailPrice),
            SvgIllustrationKey = svgIllustrationKey.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim(),
            IsFeatured = isFeatured,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        for (int size = 38; size <= 44; size++)
        {
            product.Variants.Add(new ProductVariant
            {
                Size = size,
                ColorName = defaultColorName.Trim(),
                ColorHex = defaultColorHex.Trim(),
                PhysicalStock = Math.Max(0, initialStockPerSize),
                ReservedStock = 0,
                ConcurrencyStamp = Guid.NewGuid()
            });
        }

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        await _cache.RemoveByTagAsync("catalog");
        return product;
    }

    public async Task<bool> UpdateProductAsync(
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
        string? imageUrl,
        bool isFeatured,
        bool isActive)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product is null) return false;

        product.Name = name.Trim();
        product.Category = category;
        product.ShortTagline = shortTagline.Trim();
        product.Description = description.Trim();
        product.UpperMaterial = upperMaterial.Trim();
        product.SoleMaterial = soleMaterial.Trim();
        product.ConstructionMethod = constructionMethod.Trim();
        product.RetailPrice = Math.Max(1000m, retailPrice);
        product.SvgIllustrationKey = svgIllustrationKey.Trim();
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            product.ImageUrl = imageUrl.Trim();
        }
        product.IsFeatured = isFeatured;
        product.IsActive = isActive;

        await _db.SaveChangesAsync();
        await _cache.RemoveByTagAsync("catalog");
        return true;
    }

    public async Task<bool> UpdateVariantStockAsync(int variantId, int newPhysicalStock)
    {
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == variantId);
        if (variant is null) return false;

        variant.PhysicalStock = Math.Max(variant.ReservedStock, Math.Max(0, newPhysicalStock));
        variant.ConcurrencyStamp = Guid.NewGuid();
        await _db.SaveChangesAsync();
        await _cache.RemoveByTagAsync("catalog");
        return true;
    }

    public async Task<bool> AddColorwayVariantsAsync(int productId, string colorName, string colorHex, int stockPerSize)
    {
        var product = await _db.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == productId);
        if (product is null) return false;

        var trimmedColor = colorName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedColor)) return false;

        for (int size = 38; size <= 44; size++)
        {
            if (product.Variants.Any(v => v.Size == size && v.ColorName.Equals(trimmedColor, StringComparison.OrdinalIgnoreCase)))
                continue;

            product.Variants.Add(new ProductVariant
            {
                Size = size,
                ColorName = trimmedColor,
                ColorHex = string.IsNullOrWhiteSpace(colorHex) ? "#4A2E1B" : colorHex.Trim(),
                PhysicalStock = Math.Max(0, stockPerSize),
                ReservedStock = 0,
                ConcurrencyStamp = Guid.NewGuid()
            });
        }

        await _db.SaveChangesAsync();
        await _cache.RemoveByTagAsync("catalog");
        return true;
    }

    public async Task<bool> DeleteProductAsync(int productId)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product is null) return false;

        bool hasOrders = await _db.OrderItems.AnyAsync(i => i.ProductId == productId);
        if (hasOrders)
        {
            product.IsActive = false;
        }
        else
        {
            _db.Products.Remove(product);
        }

        await _db.SaveChangesAsync();
        await _cache.RemoveByTagAsync("catalog");
        return true;
    }
}
