using System.ComponentModel.DataAnnotations;
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
    public List<TierInputModel> Tiers { get; set; } = new();

    public sealed class TierInputModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nama golongan wajib diisi."), MaxLength(80)]
        public string TierName { get; set; } = string.Empty;

        [Range(1, 10000, ErrorMessage = "Minimal pasang harus antara 1 hingga 10.000.")]
        public int MinPairs { get; set; }

        [Range(1, 100000, ErrorMessage = "Maksimal pasang tidak valid.")]
        public int? MaxPairs { get; set; }

        [Range(0, 85, ErrorMessage = "Diskon maksimal 85%.")]
        public decimal DiscountPercent { get; set; }

        public bool IsActive { get; set; }
    }

    public async Task OnGetAsync()
    {
        var dbTiers = await _pricing.GetAllTiersAsync();
        Tiers = dbTiers.Select(t => new TierInputModel
        {
            Id = t.Id,
            TierName = t.TierName,
            MinPairs = t.MinPairs,
            MaxPairs = t.MaxPairs,
            DiscountPercent = t.DiscountPercent,
            IsActive = t.IsActive
        }).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            TempData["ToastError"] = "Periksa kembali nilai aturan diskon lusinan Anda.";
            return Page();
        }

        var mapped = Tiers.Select(t => new WholesaleTierSetting
        {
            Id = t.Id,
            TierName = t.TierName,
            MinPairs = t.MinPairs,
            MaxPairs = t.MaxPairs,
            DiscountPercent = t.DiscountPercent,
            IsActive = t.IsActive
        });

        await _pricing.UpdateTiersAsync(mapped);
        TempData["ToastSuccess"] = "Aturan potongan harga lusinan B2B berhasil diperbarui.";
        return RedirectToPage();
    }
}
