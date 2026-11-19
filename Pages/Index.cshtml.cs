using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaryaJayaFootwear.Pages;

public class IndexModel : PageModel
{
    private readonly CatalogService _catalog;
    private readonly PricingEngineService _pricing;
    private readonly CartService _cart;

    public IndexModel(CatalogService catalog, PricingEngineService pricing, CartService cart)
    {
        _catalog = catalog;
        _pricing = pricing;
        _cart = cart;
    }

    public List<ProductCatalogCardViewModel> FeaturedProducts { get; private set; } = new();
    public List<StarterBundle> StarterBundles { get; private set; } = new();
    public Dictionary<FootwearCategory, int> CategoryCounts { get; private set; } = new();
    public List<WholesaleTierSetting> ActiveTiers { get; private set; } = new();
    public bool IsApprovedB2B { get; private set; }

    public async Task OnGetAsync()
    {
        IsApprovedB2B = await _cart.IsUserApprovedB2BAsync(User);
        FeaturedProducts = await _catalog.GetFeaturedProductsAsync(IsApprovedB2B, take: 6);
        StarterBundles = await _catalog.GetStarterBundlesAsync();
        CategoryCounts = await _catalog.GetCategoryCountsAsync();
        ActiveTiers = await _pricing.GetActiveTiersAsync();
    }
}
