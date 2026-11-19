using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaryaJayaFootwear.Pages.Admin;

public class PartnersModel : PageModel
{
    private readonly AuthService _auth;

    public PartnersModel(AuthService auth)
    {
        _auth = auth;
    }

    public List<User> Partners { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Partners = await _auth.GetAllB2BPartnersAsync();
    }

    public async Task<IActionResult> OnPostApproveAsync(int userId)
    {
        var ok = await _auth.SetB2BApprovalAsync(userId, approved: true);
        TempData[ok ? "ToastSuccess" : "ToastError"] = ok
            ? "Mitra Toko B2B disetujui! Harga kodian & Tempo 14 Hari kini aktif."
            : "Mitra tidak ditemukan.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int userId)
    {
        var ok = await _auth.SetB2BApprovalAsync(userId, approved: false);
        TempData[ok ? "ToastSuccess" : "ToastError"] = ok
            ? "Status Mitra B2B dinonaktifkan."
            : "Mitra tidak ditemukan.";
        return RedirectToPage();
    }
}
