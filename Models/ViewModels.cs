using System.ComponentModel.DataAnnotations;

namespace KaryaJayaFootwear.Models;

public sealed class CatalogFilterQuery
{
    public FootwearCategory? Category { get; set; }
    public int? Size { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool InStockOnly { get; set; }
    public string? Search { get; set; }
    public bool SimulateB2B { get; set; }
    public int SimulationPairs { get; set; } = 12;
}

public sealed class ProductCatalogCardViewModel
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public FootwearCategory Category { get; set; }
    public string ShortTagline { get; set; } = string.Empty;
    public string UpperMaterial { get; set; } = string.Empty;
    public string SoleMaterial { get; set; } = string.Empty;
    public decimal RetailPrice { get; set; }
    public decimal DisplayWholesalePrice { get; set; }
    public decimal DisplayDiscountPercent { get; set; }
    public string SvgIllustrationKey { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string PrimaryColorHex { get; set; } = "#4A2E1B";
    public int TotalStock { get; set; }
    public List<int> AvailableSizes { get; set; } = new();
    public bool IsFeatured { get; set; }
}

public sealed class CheckoutInputModel
{
    [Required(ErrorMessage = "Nama penerima wajib diisi.")]
    [MaxLength(120)]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email wajib diisi.")]
    [EmailAddress(ErrorMessage = "Format email tidak valid.")]
    [MaxLength(160)]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nomor WhatsApp/HP wajib diisi.")]
    [MaxLength(30)]
    public string CustomerPhone { get; set; } = string.Empty;

    [MaxLength(160)]
    public string? ShopName { get; set; }

    [Required(ErrorMessage = "Kota/Kabupaten tujuan wajib diisi.")]
    [MaxLength(100)]
    public string ShippingCity { get; set; } = string.Empty;

    [Required(ErrorMessage = "Alamat lengkap pengiriman wajib diisi.")]
    [MaxLength(350)]
    public string ShippingAddress { get; set; } = string.Empty;

    public ShippingMethod ShippingMethod { get; set; } = ShippingMethod.RegulerKurir;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.MidtransOnline;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public sealed class AdminAnalyticsDashboardViewModel
{
    public string DateRangeKey { get; set; } = "30d";
    public string DateRangeLabel { get; set; } = "30 Hari Terakhir";

    public decimal TotalRevenue { get; set; }
    public decimal B2CRevenue { get; set; }
    public decimal B2BRevenue { get; set; }
    public decimal B2CSharePercent { get; set; }
    public decimal B2BSharePercent { get; set; }

    public int TotalPairsSold { get; set; }
    public int SepatuPairsSold { get; set; }
    public int SandalPairsSold { get; set; }
    public int BootPairsSold { get; set; }

    public int TotalOrders { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int ActiveOrdersAwaitingFulfillment { get; set; }
    public decimal OutstandingB2BCreditReceivables { get; set; }
    public int PendingB2BPartnerCount { get; set; }

    public List<RevenueTrendPoint> RevenueTrend { get; set; } = new();
    public List<CategoryBreakdownItem> CategoryBreakdown { get; set; } = new();
    public List<PaymentBreakdownItem> PaymentBreakdown { get; set; } = new();
    public List<TopProductStat> TopProducts { get; set; } = new();
    public List<TopSizeStat> TopSizes { get; set; } = new();
    public List<LowStockVariantAlert> LowStockAlerts { get; set; } = new();
    public List<Order> NearDueTempoOrders { get; set; } = new();
}

public sealed class RevenueTrendPoint
{
    public string DateLabel { get; set; } = string.Empty;
    public decimal B2CRevenue { get; set; }
    public decimal B2BRevenue { get; set; }
}

public sealed class CategoryBreakdownItem
{
    public string CategoryName { get; set; } = string.Empty;
    public int PairsSold { get; set; }
    public decimal Revenue { get; set; }
}

public sealed class PaymentBreakdownItem
{
    public string PaymentLabel { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal Revenue { get; set; }
}

public sealed class TopProductStat
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int PairsSold { get; set; }
    public decimal Revenue { get; set; }
}

public sealed class TopSizeStat
{
    public int Size { get; set; }
    public int PairsSold { get; set; }
}

public sealed class LowStockVariantAlert
{
    public int VariantId { get; set; }
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Size { get; set; }
    public string ColorName { get; set; } = string.Empty;
    public int PhysicalStock { get; set; }
    public int ReservedStock { get; set; }
    public int AvailableStock { get; set; }
}
