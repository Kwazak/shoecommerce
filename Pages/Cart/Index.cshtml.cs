using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaryaJayaFootwear.Pages.Cart;

public class IndexModel : PageModel
{
    private readonly CartService _cartService;
    private readonly PricingEngineService _pricing;

    public IndexModel(CartService cartService, PricingEngineService pricing)
    {
        _cartService = cartService;
        _pricing = pricing;
    }

    public CartSummaryViewModel Summary { get; private set; } = new();
    public List<WholesaleTierSetting> Tiers { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Summary = await _cartService.GetCartSummaryAsync(User);
        Tiers = await _pricing.GetActiveTiersAsync();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int variantId, int quantity)
    {
        var res = await _cartService.UpdateQuantityAsync(variantId, quantity);
        TempData[res.Success ? "ToastInfo" : "ToastError"] = res.Message;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync(int variantId)
    {
        var res = await _cartService.RemoveItemAsync(variantId);
        TempData["ToastInfo"] = res.Message;
        return RedirectToPage();
    }
}
