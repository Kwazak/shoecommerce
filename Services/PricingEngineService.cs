using KaryaJayaFootwear.Data;
using KaryaJayaFootwear.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace KaryaJayaFootwear.Services;

public sealed class PricingEngineService
{
    private readonly AppDbContext _db;
    private readonly HybridCache _cache;

    public PricingEngineService(AppDbContext db, HybridCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<List<WholesaleTierSetting>> GetActiveTiersAsync()
    {
        return await _cache.GetOrCreateAsync(
            "wholesale-tiers:active",
            async cancel => await _db.WholesaleTierSettings
                .AsNoTracking()
                .Where(t => t.IsActive)
                .OrderBy(t => t.MinPairs)
                .ToListAsync(cancel),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(30),
                LocalCacheExpiration = TimeSpan.FromMinutes(15)
            },
            tags: new[] { "wholesale-tiers" });
    }

    public async Task<List<WholesaleTierSetting>> GetAllTiersAsync()
    {
        return await _db.WholesaleTierSettings
            .AsNoTracking()
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.MinPairs)
            .ToListAsync();
    }

    public async Task UpdateTiersAsync(IEnumerable<WholesaleTierSetting> updatedTiers)
    {
        var existing = await _db.WholesaleTierSettings.ToListAsync();
        foreach (var item in updatedTiers)
        {
            var target = existing.FirstOrDefault(e => e.Id == item.Id);
            if (target is null) continue;

            target.TierName = item.TierName.Trim();
            target.MinPairs = Math.Max(1, item.MinPairs);
            target.MaxPairs = item.MaxPairs;
            target.DiscountPercent = Math.Clamp(item.DiscountPercent, 0m, 85m);
            target.IsActive = item.IsActive;
        }

        await _db.SaveChangesAsync();
        await _cache.RemoveByTagAsync("wholesale-tiers");
    }

    public WholesaleTierSetting? ResolveTierForPairs(IReadOnlyList<WholesaleTierSetting> tiers, int totalPairs)
    {
        if (totalPairs <= 0) return null;

        return tiers
            .Where(t => t.IsActive && totalPairs >= t.MinPairs && (!t.MaxPairs.HasValue || totalPairs <= t.MaxPairs.Value))
            .OrderByDescending(t => t.DiscountPercent)
            .FirstOrDefault();
    }

    public decimal CalculateDiscountedUnitPrice(decimal retailPrice, decimal discountPercent)
    {
        if (discountPercent <= 0m) return retailPrice;
        var raw = retailPrice * (1m - (discountPercent / 100m));
        // Round to nearest Rp 100 for clean factory invoice ledger numbers
        return Math.Round(raw / 100m, MidpointRounding.AwayFromZero) * 100m;
    }

    public TierProgressInfo BuildTierProgress(IReadOnlyList<WholesaleTierSetting> tiers, int totalPairs, bool isApprovedB2B, bool isSimulation)
    {
        var ordered = tiers.Where(t => t.IsActive).OrderBy(t => t.MinPairs).ToList();
        var currentTier = ResolveTierForPairs(ordered, totalPairs);
        var nextTier = ordered.FirstOrDefault(t => t.MinPairs > totalPairs);

        int progressPercent = 100;
        if (ordered.Count > 0)
        {
            var topMin = ordered[^1].MinPairs;
            progressPercent = Math.Clamp((int)Math.Round((double)totalPairs / topMin * 100.0), 0, 100);
        }

        string banner;
        if (!isApprovedB2B && !isSimulation)
        {
            var firstTier = ordered.FirstOrDefault();
            banner = firstTier is not null
                ? $"Daftar sebagai Mitra Toko B2B untuk menikmati potongan harga kodian mulai {firstTier.DiscountPercent:0}% (min. {firstTier.MinPairs} pasang)."
                : "Daftar sebagai Mitra Toko B2B untuk harga grosir langsung dari pabrik.";
        }
        else if (currentTier is null && nextTier is not null)
        {
            int needed = nextTier.MinPairs - totalPairs;
            banner = $"Tambah {needed} pasang lagi untuk masuk {nextTier.TierName} (Diskon {nextTier.DiscountPercent:0}%).";
        }
        else if (currentTier is not null && nextTier is not null)
        {
            int needed = nextTier.MinPairs - totalPairs;
            banner = $"Aktif: {currentTier.TierName} ({currentTier.DiscountPercent:0}% OFF). Tambah {needed} pasang lagi untuk {nextTier.TierName} ({nextTier.DiscountPercent:0}% OFF)!";
        }
        else if (currentTier is not null)
        {
            banner = $"Tingkat Grosir Tertinggi Aktif: {currentTier.TierName} — Potongan Pabrik {currentTier.DiscountPercent:0}%!";
        }
        else
        {
            banner = "Harga Pabrik Langsung Tanpa Perantara.";
        }

        return new TierProgressInfo
        {
            CurrentTierName = currentTier?.TierName,
            CurrentDiscountPercent = currentTier?.DiscountPercent ?? 0m,
            NextTierName = nextTier?.TierName,
            NextDiscountPercent = nextTier?.DiscountPercent,
            PairsNeededForNextTier = nextTier is not null ? Math.Max(0, nextTier.MinPairs - totalPairs) : null,
            ProgressPercent = progressPercent,
            BannerMessage = banner
        };
    }

    public decimal CalculateShippingCost(ShippingMethod method, int totalPairs, bool isWholesaleOrder)
    {
        if (totalPairs <= 0) return 0m;

        return method switch
        {
            ShippingMethod.EkspedisiKargoTruk => isWholesaleOrder
                ? 45000m + (Math.Max(0, totalPairs - 6) * 1500m)
                : 65000m,
            _ => 18000m + (Math.Max(0, totalPairs - 1) * 4000m)
        };
    }
}
