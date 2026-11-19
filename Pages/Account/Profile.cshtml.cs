using System.ComponentModel.DataAnnotations;
using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaryaJayaFootwear.Pages.Account;

public class ProfileModel : PageModel
{
    private readonly AuthService _auth;

    public ProfileModel(AuthService auth)
    {
        _auth = auth;
    }

    public User CurrentUser { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Nama lengkap wajib diisi.")]
        [MaxLength(120)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nomor WhatsApp/HP wajib diisi.")]
        [MaxLength(30)]
        public string PhoneNumber { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(300)]
        public string? ShopAddress { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _auth.GetCurrentUserAsync(User);
        if (user is null) return RedirectToPage("/Account/Login");

        CurrentUser = user;
        Input.FullName = user.FullName;
        Input.PhoneNumber = user.PhoneNumber;
        Input.City = user.City;
        Input.ShopAddress = user.ShopAddress;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _auth.GetCurrentUserAsync(User);
        if (user is null) return RedirectToPage("/Account/Login");
        CurrentUser = user;

        if (!ModelState.IsValid) return Page();

        await _auth.UpdateProfileAsync(user.Id, Input.FullName, Input.PhoneNumber, Input.City, Input.ShopAddress);
        TempData["ToastSuccess"] = "Catatan profil berhasil diperbarui.";
        return RedirectToPage();
    }
}
