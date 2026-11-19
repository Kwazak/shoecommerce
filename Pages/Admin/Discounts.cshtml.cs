using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaryaJayaFootwear.Pages.Admin;

public class DiscountsModel : PageModel
{
    private readonly PricingEngineService _pricing;

    public DiscountsModel(PricingEngineService pricing)
    {
        _pricing = pricing;
    }

    [BindProperty]
    public List<WholesaleTierSetting> Tiers { get; set; } = new();

    public async Task OnGetAsync()
    {
        Tiers = await _pricing.GetAllTiersAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _pricing.UpdateTiersAsync(Tiers);
        TempData["ToastSuccess"] = "Aturan potongan harga kodian B2B berhasil diperbarui di SQLite.";
        return RedirectToPage();
    }
}
