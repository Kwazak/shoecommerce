namespace KaryaJayaFootwear.Models;

public sealed class CartCookieEntry
{
    public int VariantId { get; set; }
    public int Quantity { get; set; }
}

public sealed class CartItemViewModel
{
    public int VariantId { get; set; }
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public FootwearCategory Category { get; set; }
    public string SvgIllustrationKey { get; set; } = string.Empty;
    public int Size { get; set; }
    public string ColorName { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#4A2E1B";
    public int Quantity { get; set; }
    public int AvailableStock { get; set; }
    public decimal UnitRetailPrice { get; set; }
    public decimal UnitAppliedPrice { get; set; }
    public decimal LineRetailTotal { get; set; }
    public decimal LineAppliedTotal { get; set; }
}

public sealed class TierProgressInfo
{
    public string? CurrentTierName { get; set; }
    public decimal CurrentDiscountPercent { get; set; }
    public string? NextTierName { get; set; }
    public decimal? NextDiscountPercent { get; set; }
    public int? PairsNeededForNextTier { get; set; }
    public int ProgressPercent { get; set; }
    public string BannerMessage { get; set; } = string.Empty;
}

public sealed class CartSummaryViewModel
{
    public List<CartItemViewModel> Items { get; set; } = new();
    public int TotalPairs { get; set; }
    public bool IsWholesalePricingActive { get; set; }
    public bool IsB2BSimulation { get; set; }
    public decimal GrossRetailSubtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetSubtotal { get; set; }
    public TierProgressInfo TierProgress { get; set; } = new();
}
