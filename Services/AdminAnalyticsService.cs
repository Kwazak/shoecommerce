using KaryaJayaFootwear.Data;
using KaryaJayaFootwear.Models;
using Microsoft.EntityFrameworkCore;

namespace KaryaJayaFootwear.Services;

public sealed class AdminAnalyticsService
{
    private readonly AppDbContext _db;

    public AdminAnalyticsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AdminAnalyticsDashboardViewModel> GetDashboardAsync(string? dateRangeKey)
    {
        var key = string.IsNullOrWhiteSpace(dateRangeKey) ? "30d" : dateRangeKey.Trim().ToLowerInvariant();
        var nowUtc = DateTime.UtcNow;
        var wibTodayStartUtc = nowUtc.AddHours(7).Date.AddHours(-7);

        (DateTime? startUtc, string label) = key switch
        {
            "today" => (wibTodayStartUtc, "Hari Ini"),
            "7d" => (wibTodayStartUtc.AddDays(-6), "7 Hari Terakhir"),
            "30d" => (wibTodayStartUtc.AddDays(-29), "30 Hari Terakhir"),
            "all" => ((DateTime?)null, "Semua Waktu"),
            _ => (wibTodayStartUtc.AddDays(-29), "30 Hari Terakhir")
        };

        if (key is not ("today" or "7d" or "30d" or "all"))
        {
            key = "30d";
        }

        var ordersQuery = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.OrderStatus != OrderStatus.Dibatalkan && o.OrderStatus != OrderStatus.Kedaluwarsa);

        if (startUtc.HasValue)
        {
            ordersQuery = ordersQuery.Where(o => o.CreatedAt >= startUtc.Value);
        }

        var orders = await ordersQuery
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        decimal totalRevenue = orders.Sum(o => o.GrandTotal);
        decimal b2cRevenue = orders.Where(o => !o.IsWholesaleOrder).Sum(o => o.GrandTotal);
        decimal b2bRevenue = orders.Where(o => o.IsWholesaleOrder).Sum(o => o.GrandTotal);

        decimal b2cShare = totalRevenue > 0 ? Math.Round(b2cRevenue / totalRevenue * 100m, 1) : 0m;
        decimal b2bShare = totalRevenue > 0 ? Math.Round(100m - b2cShare, 1) : 0m;

        var allItems = orders.SelectMany(o => o.Items).ToList();
        int totalPairsSold = allItems.Sum(i => i.Quantity);
        int sepatuPairs = allItems.Where(i => i.Category == FootwearCategory.Sepatu).Sum(i => i.Quantity);
        int sandalPairs = allItems.Where(i => i.Category == FootwearCategory.Sandal).Sum(i => i.Quantity);
        int bootPairs = allItems.Where(i => i.Category == FootwearCategory.Boot).Sum(i => i.Quantity);

        int totalOrders = orders.Count;
        decimal avgOrderValue = totalOrders > 0 ? Math.Round(totalRevenue / totalOrders, 0) : 0m;

        // Active operational counts (all-time operational queue)
        int awaitingFulfillment = await _db.Orders
            .AsNoTracking()
            .CountAsync(o => o.OrderStatus == OrderStatus.MenungguPembayaran ||
                             o.OrderStatus == OrderStatus.DibayarSiapDikemas ||
                             o.OrderStatus == OrderStatus.DijahitDikemas);

        decimal outstandingReceivables = await _db.Orders
            .AsNoTracking()
            .Where(o => o.PaymentMethod == PaymentMethod.TempoB2B &&
                        o.OrderStatus != OrderStatus.Dibatalkan &&
                        o.OrderStatus != OrderStatus.Kedaluwarsa &&
                        o.PaidAt == null)
            .SumAsync(o => (decimal?)o.GrandTotal) ?? 0m;

        int pendingPartners = await _db.Users
            .AsNoTracking()
            .CountAsync(u => u.Role == UserRole.PartnerB2B && !u.IsB2BApproved);

        // Revenue Trend grouped by WIB date
        var trendPoints = BuildRevenueTrend(orders, key, startUtc);

        // Category breakdown
        var categoryBreakdown = Enum.GetValues<FootwearCategory>()
            .Select(cat => new CategoryBreakdownItem
            {
                CategoryName = cat.ToIndonesianLabel(),
                PairsSold = allItems.Where(i => i.Category == cat).Sum(i => i.Quantity),
                Revenue = allItems.Where(i => i.Category == cat).Sum(i => i.LineTotal)
            })
            .ToList();

        // Payment breakdown
        var paymentBreakdown = Enum.GetValues<PaymentMethod>()
            .Select(pm => new PaymentBreakdownItem
            {
                PaymentLabel = pm switch
                {
                    PaymentMethod.MidtransOnline => "Midtrans Online",
                    PaymentMethod.COD => "COD",
                    PaymentMethod.TempoB2B => "Tempo B2B",
                    _ => pm.ToString()
                },
                OrderCount = orders.Count(o => o.PaymentMethod == pm),
                Revenue = orders.Where(o => o.PaymentMethod == pm).Sum(o => o.GrandTotal)
            })
            .ToList();

        // Top 5 Products
        var topProducts = allItems
            .GroupBy(i => new { i.ProductId, i.ProductSku, i.ProductName, i.Category })
            .Select(g => new TopProductStat
            {
                ProductId = g.Key.ProductId,
                Sku = g.Key.ProductSku,
                ProductName = g.Key.ProductName,
                CategoryName = g.Key.Category.ToIndonesianLabel(),
                PairsSold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.LineTotal)
            })
            .OrderByDescending(x => x.PairsSold)
            .ThenByDescending(x => x.Revenue)
            .Take(5)
            .ToList();

        // Most demanded sizes (38-44)
        var topSizes = Enumerable.Range(38, 7)
            .Select(sz => new TopSizeStat
            {
                Size = sz,
                PairsSold = allItems.Where(i => i.Size == sz).Sum(i => i.Quantity)
            })
            .OrderByDescending(s => s.PairsSold)
            .ThenBy(s => s.Size)
            .ToList();

        // Low-Stock Alerts (< 5 available pairs)
        var lowStockAlerts = await _db.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
            .Where(v => v.Product.IsActive && (v.PhysicalStock - v.ReservedStock) < 5)
            .OrderBy(v => v.PhysicalStock - v.ReservedStock)
            .ThenBy(v => v.Product.Name)
            .Select(v => new LowStockVariantAlert
            {
                VariantId = v.Id,
                ProductId = v.ProductId,
                Sku = v.Product.Sku,
                ProductName = v.Product.Name,
                Size = v.Size,
                ColorName = v.ColorName,
                PhysicalStock = v.PhysicalStock,
                ReservedStock = v.ReservedStock,
                AvailableStock = v.PhysicalStock - v.ReservedStock
            })
            .Take(15)
            .ToListAsync();

        var nearDueTempoOrders = await _db.Orders
            .AsNoTracking()
            .Where(o => o.IsTempoNearDue &&
                        o.PaymentMethod == PaymentMethod.TempoB2B &&
                        o.PaidAt == null &&
                        o.OrderStatus != OrderStatus.Dibatalkan &&
                        o.OrderStatus != OrderStatus.Kedaluwarsa)
            .OrderBy(o => o.TempoDueDate)
            .ToListAsync();

        return new AdminAnalyticsDashboardViewModel
        {
            DateRangeKey = key,
            DateRangeLabel = label,
            TotalRevenue = totalRevenue,
            B2CRevenue = b2cRevenue,
            B2BRevenue = b2bRevenue,
            B2CSharePercent = b2cShare,
            B2BSharePercent = b2bShare,
            TotalPairsSold = totalPairsSold,
            SepatuPairsSold = sepatuPairs,
            SandalPairsSold = sandalPairs,
            BootPairsSold = bootPairs,
            TotalOrders = totalOrders,
            AverageOrderValue = avgOrderValue,
            ActiveOrdersAwaitingFulfillment = awaitingFulfillment,
            OutstandingB2BCreditReceivables = outstandingReceivables,
            PendingB2BPartnerCount = pendingPartners,
            RevenueTrend = trendPoints,
            CategoryBreakdown = categoryBreakdown,
            PaymentBreakdown = paymentBreakdown,
            TopProducts = topProducts,
            TopSizes = topSizes,
            LowStockAlerts = lowStockAlerts,
            NearDueTempoOrders = nearDueTempoOrders
        };
    }

    private static List<RevenueTrendPoint> BuildRevenueTrend(List<Order> orders, string rangeKey, DateTime? startUtc)
    {
        var todayWib = DateTime.UtcNow.AddHours(7).Date;
        var byDay = orders
            .GroupBy(o => o.CreatedAt.AddHours(7).Date)
            .ToDictionary(
                g => g.Key,
                g => (
                    B2C: g.Where(x => !x.IsWholesaleOrder).Sum(x => x.GrandTotal),
                    B2B: g.Where(x => x.IsWholesaleOrder).Sum(x => x.GrandTotal)
                ));

        if (rangeKey == "today")
        {
            byDay.TryGetValue(todayWib, out var todayVal);
            return new List<RevenueTrendPoint>
            {
                new()
                {
                    DateLabel = todayWib.ToString("dd MMM"),
                    B2CRevenue = todayVal.B2C,
                    B2BRevenue = todayVal.B2B
                }
            };
        }

        DateTime firstDay;
        if (rangeKey == "7d")
        {
            firstDay = todayWib.AddDays(-6);
        }
        else if (rangeKey == "30d")
        {
            firstDay = todayWib.AddDays(-29);
        }
        else
        {
            firstDay = byDay.Count > 0 ? byDay.Keys.Min() : todayWib.AddDays(-6);
        }

        var points = new List<RevenueTrendPoint>();
        for (var day = firstDay; day <= todayWib; day = day.AddDays(1))
        {
            byDay.TryGetValue(day, out var val);
            points.Add(new RevenueTrendPoint
            {
                DateLabel = day.ToString("dd/MM"),
                B2CRevenue = val.B2C,
                B2BRevenue = val.B2B
            });
        }

        return points;
    }
}
