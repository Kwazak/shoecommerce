using System.Security.Claims;
using System.Text.Json;
using KaryaJayaFootwear.Data;
using KaryaJayaFootwear.Models;
using Microsoft.EntityFrameworkCore;

namespace KaryaJayaFootwear.Services;

public sealed class CartActionResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public CartSummaryViewModel Summary { get; init; } = new();
}

public sealed class CartService
{
    private const string CartCookieName = "KaryaJayaCartV1";
    private readonly AppDbContext _db;
    private readonly PricingEngineService _pricing;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CartService(
        AppDbContext db,
        PricingEngineService pricing,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _pricing = pricing;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<CartSummaryViewModel> GetCartSummaryAsync(ClaimsPrincipal? principal = null, bool simulateB2B = false)
    {
        var entries = ReadCookieEntries();
        var user = principal ?? _httpContextAccessor.HttpContext?.User;
        bool isApprovedB2B = await IsUserApprovedB2BAsync(user);
        bool wholesaleActive = isApprovedB2B || simulateB2B;

        var tiers = await _pricing.GetActiveTiersAsync();

        if (entries.Count == 0)
        {
            return new CartSummaryViewModel
            {
                IsWholesalePricingActive = wholesaleActive,
                IsB2BSimulation = !isApprovedB2B && simulateB2B,
                TierProgress = _pricing.BuildTierProgress(tiers, 0, isApprovedB2B, simulateB2B)
            };
        }

        var variantIds = entries.Select(e => e.VariantId).Distinct().ToList();
        var variants = await _db.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
            .Where(v => variantIds.Contains(v.Id) && v.Product.IsActive)
            .ToDictionaryAsync(v => v.Id);

        var rawItems = new List<(ProductVariant Variant, int Qty)>();
        bool cookieDirty = false;
        var cleanedEntries = new List<CartCookieEntry>();

        foreach (var entry in entries)
        {
            if (!variants.TryGetValue(entry.VariantId, out var variant) || variant.AvailableStock <= 0)
            {
                cookieDirty = true;
                continue;
            }

            int clampedQty = Math.Clamp(entry.Quantity, 1, variant.AvailableStock);
            if (clampedQty != entry.Quantity) cookieDirty = true;

            rawItems.Add((variant, clampedQty));
            cleanedEntries.Add(new CartCookieEntry { VariantId = entry.VariantId, Quantity = clampedQty });
        }

        if (cookieDirty)
        {
            WriteCookieEntries(cleanedEntries);
        }

        int totalPairs = rawItems.Sum(x => x.Qty);
        var activeTier = wholesaleActive ? _pricing.ResolveTierForPairs(tiers, totalPairs) : null;
        decimal discountPct = activeTier?.DiscountPercent ?? 0m;

        var viewItems = rawItems.Select(x =>
        {
            decimal retailUnit = x.Variant.Product.RetailPrice;
            decimal appliedUnit = discountPct > 0m
                ? _pricing.CalculateDiscountedUnitPrice(retailUnit, discountPct)
                : retailUnit;

            return new CartItemViewModel
            {
                VariantId = x.Variant.Id,
                ProductId = x.Variant.ProductId,
                Sku = x.Variant.Product.Sku,
                ProductName = x.Variant.Product.Name,
                Category = x.Variant.Product.Category,
                SvgIllustrationKey = x.Variant.Product.SvgIllustrationKey,
                Size = x.Variant.Size,
                ColorName = x.Variant.ColorName,
                ColorHex = x.Variant.ColorHex,
                Quantity = x.Qty,
                AvailableStock = Math.Max(0, x.Variant.AvailableStock),
                UnitRetailPrice = retailUnit,
                UnitAppliedPrice = appliedUnit,
                LineRetailTotal = retailUnit * x.Qty,
                LineAppliedTotal = appliedUnit * x.Qty
            };
        }).ToList();

        decimal grossRetail = viewItems.Sum(i => i.LineRetailTotal);
        decimal netSubtotal = viewItems.Sum(i => i.LineAppliedTotal);
        decimal discountAmount = Math.Max(0m, grossRetail - netSubtotal);

        return new CartSummaryViewModel
        {
            Items = viewItems,
            TotalPairs = totalPairs,
            IsWholesalePricingActive = wholesaleActive,
            IsB2BSimulation = !isApprovedB2B && simulateB2B,
            GrossRetailSubtotal = grossRetail,
            DiscountPercent = discountPct,
            DiscountAmount = discountAmount,
            NetSubtotal = netSubtotal,
            TierProgress = _pricing.BuildTierProgress(tiers, totalPairs, isApprovedB2B, simulateB2B)
        };
    }

    public async Task<CartActionResult> AddToCartAsync(int variantId, int quantity)
    {
        if (quantity <= 0)
        {
            return new CartActionResult
            {
                Success = false,
                Message = "Jumlah pasang minimal 1.",
                Summary = await GetCartSummaryAsync()
            };
        }

        var variant = await _db.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId && v.Product.IsActive);

        if (variant is null)
        {
            return new CartActionResult
            {
                Success = false,
                Message = "Varian sepatu tidak ditemukan di gudang.",
                Summary = await GetCartSummaryAsync()
            };
        }

        if (variant.AvailableStock <= 0)
        {
            return new CartActionResult
            {
                Success = false,
                Message = $"Stok tersedia {variant.Product.Name} ukuran {variant.Size} ({variant.ColorName}) sedang habis.",
                Summary = await GetCartSummaryAsync()
            };
        }

        var entries = ReadCookieEntries();
        var existing = entries.FirstOrDefault(e => e.VariantId == variantId);
        int desired = (existing?.Quantity ?? 0) + quantity;
        if (desired > variant.AvailableStock)
        {
            desired = variant.AvailableStock;
        }

        if (existing is null)
        {
            entries.Add(new CartCookieEntry { VariantId = variantId, Quantity = desired });
        }
        else
        {
            existing.Quantity = desired;
        }

        WriteCookieEntries(entries);

        var summary = await GetCartSummaryAsync();
        return new CartActionResult
        {
            Success = true,
            Message = $"{variant.Product.Name} (Uk. {variant.Size} • {variant.ColorName}) masuk Laci Kasir!",
            Summary = summary
        };
    }

    public async Task<CartActionResult> AddBatchItemsAsync(int productId, Dictionary<int, int> variantQuantities)
    {
        if (variantQuantities.Values.Any(q => q < 0))
        {
            return new CartActionResult
            {
                Success = false,
                Message = "Jumlah pesanan tidak boleh bernilai negatif.",
                Summary = await GetCartSummaryAsync()
            };
        }

        var requested = variantQuantities
            .Where(kv => kv.Value > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        if (requested.Count == 0)
        {
            return new CartActionResult
            {
                Success = false,
                Message = "Isi minimal 1 pasang pada matriks ukuran terlebih dahulu.",
                Summary = await GetCartSummaryAsync()
            };
        }

        var variantIds = requested.Keys.ToList();
        var dbVariants = await _db.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
            .Where(v => v.ProductId == productId && v.Product.IsActive && variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id);

        if (dbVariants.Count == 0)
        {
            return new CartActionResult
            {
                Success = false,
                Message = "Varian produk tidak ditemukan atau sudah tidak aktif.",
                Summary = await GetCartSummaryAsync()
            };
        }

        var entries = ReadCookieEntries();
        int totalAddedPairs = 0;

        foreach (var (variantId, qty) in requested)
        {
            if (!dbVariants.TryGetValue(variantId, out var variant))
            {
                return new CartActionResult
                {
                    Success = false,
                    Message = "Salah satu varian ukuran tidak valid untuk produk ini.",
                    Summary = await GetCartSummaryAsync()
                };
            }

            if (variant.AvailableStock <= 0)
            {
                return new CartActionResult
                {
                    Success = false,
                    Message = $"Stok tersedia {variant.Product.Name} Uk. {variant.Size} ({variant.ColorName}) sudah habis.",
                    Summary = await GetCartSummaryAsync()
                };
            }

            var existing = entries.FirstOrDefault(e => e.VariantId == variantId);
            int desired = (existing?.Quantity ?? 0) + qty;
            if (desired > variant.AvailableStock)
            {
                return new CartActionResult
                {
                    Success = false,
                    Message = $"Permintaan Uk. {variant.Size} ({variant.ColorName}) melebihi stok tersedia ({variant.AvailableStock} pasang).",
                    Summary = await GetCartSummaryAsync()
                };
            }

            if (existing is null)
            {
                entries.Add(new CartCookieEntry { VariantId = variantId, Quantity = desired });
            }
            else
            {
                existing.Quantity = desired;
            }

            totalAddedPairs += qty;
        }

        WriteCookieEntries(entries);
        var summary = await GetCartSummaryAsync();

        return new CartActionResult
        {
            Success = true,
            Message = $"{totalAddedPairs} pasang ({requested.Count} varian ukuran) ditambahkan ke Laci Kasir Grosir!",
            Summary = summary
        };
    }

    public async Task<CartActionResult> AddStarterBundleAsync(int bundleId)
    {
        if (bundleId <= 0)
        {
            return new CartActionResult
            {
                Success = false,
                Message = "ID Paket Usaha tidak valid.",
                Summary = await GetCartSummaryAsync()
            };
        }

        var bundle = await _db.StarterBundles
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bundleId && b.IsActive);

        if (bundle is null)
        {
            return new CartActionResult
            {
                Success = false,
                Message = "Paket Usaha Pemula tidak ditemukan.",
                Summary = await GetCartSummaryAsync()
            };
        }

        Dictionary<int, int>? allocations;
        try
        {
            allocations = JsonSerializer.Deserialize<Dictionary<int, int>>(bundle.VariantAllocationsJson);
        }
        catch
        {
            allocations = null;
        }

        if (allocations is null || allocations.Count == 0)
        {
            return new CartActionResult
            {
                Success = false,
                Message = "Konfigurasi varian Paket Usaha belum lengkap.",
                Summary = await GetCartSummaryAsync()
            };
        }

        var variantIds = allocations.Keys.ToList();
        var dbVariants = await _db.ProductVariants
            .AsNoTracking()
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id);

        var entries = ReadCookieEntries();
        foreach (var (variantId, qty) in allocations)
        {
            if (qty <= 0 || !dbVariants.TryGetValue(variantId, out var variant) || variant.AvailableStock <= 0)
            {
                continue;
            }

            var existing = entries.FirstOrDefault(e => e.VariantId == variantId);
            int desired = Math.Min((existing?.Quantity ?? 0) + qty, variant.AvailableStock);
            if (existing is null)
            {
                entries.Add(new CartCookieEntry { VariantId = variantId, Quantity = desired });
            }
            else
            {
                existing.Quantity = desired;
            }
        }

        WriteCookieEntries(entries);
        var summary = await GetCartSummaryAsync();

        return new CartActionResult
        {
            Success = true,
            Message = $"Paket Usaha '{bundle.Name}' ({bundle.TotalPairs} pasang) masuk Laci Kasir!",
            Summary = summary
        };
    }

    public async Task<CartActionResult> UpdateQuantityAsync(int variantId, int quantity)
    {
        var entries = ReadCookieEntries();
        var existing = entries.FirstOrDefault(e => e.VariantId == variantId);
        if (existing is null)
        {
            return new CartActionResult
            {
                Success = false,
                Message = "Item tidak ada dalam keranjang.",
                Summary = await GetCartSummaryAsync()
            };
        }

        if (quantity <= 0)
        {
            return new CartActionResult
            {
                Success = false,
                Message = "Jumlah pasang minimal 1. Gunakan tombol Hapus untuk mengeluarkan barang.",
                Summary = await GetCartSummaryAsync()
            };
        }

        var variant = await _db.ProductVariants.AsNoTracking().FirstOrDefaultAsync(v => v.Id == variantId);
        if (variant is null || variant.AvailableStock <= 0)
        {
            entries.Remove(existing);
            WriteCookieEntries(entries);
            return new CartActionResult
            {
                Success = false,
                Message = "Stok varian habis dan telah disesuaikan.",
                Summary = await GetCartSummaryAsync()
            };
        }

        existing.Quantity = Math.Min(quantity, variant.AvailableStock);
        WriteCookieEntries(entries);

        return new CartActionResult
        {
            Success = true,
            Message = quantity > variant.AvailableStock
                ? $"Jumlah disesuaikan ke sisa stok tersedia ({variant.AvailableStock} pasang)."
                : "Jumlah pasang diperbarui.",
            Summary = await GetCartSummaryAsync()
        };
    }

    public async Task<CartActionResult> RemoveItemAsync(int variantId)
    {
        var entries = ReadCookieEntries();
        entries.RemoveAll(e => e.VariantId == variantId);
        WriteCookieEntries(entries);

        return new CartActionResult
        {
            Success = true,
            Message = "Item dicoret dari daftar belanja.",
            Summary = await GetCartSummaryAsync()
        };
    }

    public void ClearCart()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null) return;
        ctx.Response.Cookies.Delete(CartCookieName);
    }

    public async Task<bool> IsUserApprovedB2BAsync(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true) return false;

        var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idClaim, out var userId)) return false;

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        return user is not null && user.Role == UserRole.PartnerB2B && user.IsB2BApproved;
    }

    private List<CartCookieEntry> ReadCookieEntries()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null) return new List<CartCookieEntry>();

        if (!ctx.Request.Cookies.TryGetValue(CartCookieName, out var json) || string.IsNullOrWhiteSpace(json))
        {
            return new List<CartCookieEntry>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<CartCookieEntry>>(json) ?? new List<CartCookieEntry>();
        }
        catch
        {
            return new List<CartCookieEntry>();
        }
    }

    private void WriteCookieEntries(List<CartCookieEntry> entries)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null) return;

        if (entries.Count == 0)
        {
            ctx.Response.Cookies.Delete(CartCookieName);
            return;
        }

        var json = JsonSerializer.Serialize(entries);
        ctx.Response.Cookies.Append(CartCookieName, json, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(14)
        });
    }
}
