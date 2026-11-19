using System.ComponentModel.DataAnnotations;
using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaryaJayaFootwear.Pages.Account;

public class LoginModel : PageModel
{
    private readonly AuthService _auth;

    public LoginModel(AuthService auth)
    {
        _auth = auth;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Alamat email wajib diisi.")]
        [EmailAddress(ErrorMessage = "Format email tidak valid.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kata sandi wajib diisi.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; } = true;
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _auth.ValidateLoginAsync(Input.Email, Input.Password);
        if (!result.Success || result.User is null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Gagal masuk.");
            return Page();
        }

        await _auth.SignInUserAsync(HttpContext, result.User, Input.RememberMe);
        TempData["ToastSuccess"] = $"Selamat datang kembali, {result.User.FullName}!";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return result.User.Role == UserRole.Admin
            ? RedirectToPage("/Admin/Index")
            : RedirectToPage("/Account/Orders");
    }
}
