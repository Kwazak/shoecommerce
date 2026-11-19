using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaryaJayaFootwear.Pages.Products;

public class DetailsModel : PageModel
{
    private readonly CatalogService _catalog;
    private readonly PricingEngineService _pricing;
    private readonly CartService _cart;

    public DetailsModel(CatalogService catalog, PricingEngineService pricing, CartService cart)
    {
        _catalog = catalog;
        _pricing = pricing;
        _cart = cart;
    }

    public Product Product { get; private set; } = null!;
    public List<WholesaleTierSetting> ActiveTiers { get; private set; } = new();
    public bool IsApprovedB2B { get; private set; }

    [BindProperty(SupportsGet = true)]
    public bool SimulateB2B { get; set; }

    public bool ShowB2BMatrix => IsApprovedB2B || SimulateB2B;
    public List<(string ColorName, string ColorHex)> Colorways { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var product = await _catalog.GetProductDetailsAsync(id);
        if (product is null || !product.IsActive)
        {
            return NotFound();
        }

        Product = product;
        IsApprovedB2B = await _cart.IsUserApprovedB2BAsync(User);
        ActiveTiers = await _pricing.GetActiveTiersAsync();
        Colorways = product.Variants
            .Select(v => (v.ColorName, v.ColorHex))
            .Distinct()
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAddToCartAsync(int id, int variantId, int quantity)
    {
        var result = await _cart.AddToCartAsync(variantId, Math.Max(1, quantity));
        if (result.Success)
        {
            TempData["ToastSuccess"] = result.Message;
            TempData["OpenCartDrawer"] = "true";
        }
        else
        {
            TempData["ToastError"] = result.Message;
        }
        return RedirectToPage(new { id, SimulateB2B });
    }

    public async Task<IActionResult> OnPostAddBatchAsync(int id, Dictionary<int, int> variantQuantities, bool simulateB2B = false)
    {
        var result = await _cart.AddBatchItemsAsync(id, variantQuantities ?? new Dictionary<int, int>());
        if (result.Success)
        {
            TempData["ToastSuccess"] = result.Message;
            TempData["OpenCartDrawer"] = "true";
        }
        else
        {
            TempData["ToastError"] = result.Message;
        }

        return RedirectToPage(new { id, SimulateB2B = simulateB2B });
    }
}
