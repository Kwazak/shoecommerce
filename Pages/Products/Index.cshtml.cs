using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaryaJayaFootwear.Pages.Products;

public class IndexModel : PageModel
{
    private readonly CatalogService _catalog;
    private readonly CartService _cart;
    private readonly PricingEngineService _pricing;

    public IndexModel(CatalogService catalog, CartService cart, PricingEngineService pricing)
    {
        _catalog = catalog;
        _cart = cart;
        _pricing = pricing;
    }

    [BindProperty(SupportsGet = true)]
    public CatalogFilterQuery Filter { get; set; } = new();

    public List<ProductCatalogCardViewModel> Products { get; private set; } = new();
    public List<StarterBundle> StarterBundles { get; private set; } = new();
    public List<WholesaleTierSetting> ActiveTiers { get; private set; } = new();
    public bool IsApprovedB2B { get; private set; }

    public async Task OnGetAsync()
    {
        IsApprovedB2B = await _cart.IsUserApprovedB2BAsync(User);
        if (Filter.SimulationPairs < 6) Filter.SimulationPairs = 12;

        Products = await _catalog.GetCatalogAsync(Filter, IsApprovedB2B);
        StarterBundles = await _catalog.GetStarterBundlesAsync();
        ActiveTiers = await _pricing.GetActiveTiersAsync();
    }
}
