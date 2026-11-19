using System.ComponentModel.DataAnnotations;
using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaryaJayaFootwear.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly AuthService _auth;

    public RegisterModel(AuthService auth)
    {
        _auth = auth;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        [Required]
        public UserRole AccountType { get; set; } = UserRole.CustomerB2C;

        [Required(ErrorMessage = "Nama lengkap wajib diisi.")]
        [MaxLength(120)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Alamat email wajib diisi.")]
        [EmailAddress(ErrorMessage = "Format email tidak valid.")]
        [MaxLength(160)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nomor WhatsApp/HP wajib diisi.")]
        [MaxLength(30)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kata sandi wajib diisi.")]
        [MinLength(6, ErrorMessage = "Kata sandi minimal 6 karakter.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Konfirmasi kata sandi wajib diisi.")]
        [Compare(nameof(Password), ErrorMessage = "Konfirmasi kata sandi tidak cocok.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;

        [MaxLength(160)]
        public string? ShopName { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(300)]
        public string? ShopAddress { get; set; }
    }

    public void OnGet(string? type = null)
    {
        if (string.Equals(type, nameof(UserRole.PartnerB2B), StringComparison.OrdinalIgnoreCase))
        {
            Input.AccountType = UserRole.PartnerB2B;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var res = await _auth.RegisterAsync(new RegisterRequest
        {
            AccountType = Input.AccountType,
            FullName = Input.FullName,
            Email = Input.Email,
            PhoneNumber = Input.PhoneNumber,
            Password = Input.Password,
            ShopName = Input.ShopName,
            City = Input.City,
            ShopAddress = Input.ShopAddress
        });

        if (!res.Success || res.User is null)
        {
            ModelState.AddModelError(string.Empty, res.ErrorMessage ?? "Pendaftaran gagal.");
            return Page();
        }

        await _auth.SignInUserAsync(HttpContext, res.User, rememberMe: true);

        if (res.User.Role == UserRole.PartnerB2B)
        {
            TempData["PartnerPendingNotice"] =
                "Pendaftaran Mitra Berhasil — Menunggu Verifikasi Admin Pabrik untuk Aktivasi Harga Kodian & Tempo";
            return RedirectToPage("/Account/Orders");
        }

        TempData["ToastSuccess"] = "Pendaftaran Pembeli Eceran berhasil! Selamat berbelanja langsung dari pabrik.";
        return RedirectToPage("/Account/Orders");
    }
}
