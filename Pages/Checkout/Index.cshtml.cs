using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaryaJayaFootwear.Pages.Checkout;

public class IndexModel : PageModel
{
    private readonly CartService _cartService;
    private readonly AuthService _authService;
    private readonly PricingEngineService _pricing;
    private readonly OrderFulfillmentService _fulfillment;

    public IndexModel(
        CartService cartService,
        AuthService authService,
        PricingEngineService pricing,
        OrderFulfillmentService fulfillment)
    {
        _cartService = cartService;
        _authService = authService;
        _pricing = pricing;
        _fulfillment = fulfillment;
    }

    [BindProperty]
    public CheckoutInputModel Input { get; set; } = new();

    public CartSummaryViewModel Cart { get; private set; } = new();
    public bool IsApprovedB2B { get; private set; }
    public bool IsPendingB2B { get; private set; }
    public bool IsAdminUser { get; private set; }
    public decimal RegularShippingCost { get; private set; }
    public decimal CargoShippingCost { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadPageContextAsync();
        if (Cart.Items.Count == 0)
        {
            TempData["ToastInfo"] = "Laci Kasir masih kosong. Silakan pilih produk terlebih dahulu.";
            return RedirectToPage("/Products/Index");
        }

        var user = await _authService.GetCurrentUserAsync(User);
        if (user is not null)
        {
            Input.CustomerName = user.FullName;
            Input.CustomerEmail = user.Email;
            Input.CustomerPhone = user.PhoneNumber;
            Input.ShopName = user.ShopName;
            Input.ShippingCity = user.City ?? string.Empty;
            Input.ShippingAddress = user.ShopAddress ?? string.Empty;

            if (IsApprovedB2B)
            {
                Input.ShippingMethod = ShippingMethod.EkspedisiKargoTruk;
                Input.PaymentMethod = PaymentMethod.TempoB2B;
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadPageContextAsync();
        if (IsAdminUser)
        {
            ModelState.AddModelError(string.Empty, "Akun Admin tidak diperbolehkan membuat pesanan pelanggan.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var result = await _fulfillment.PlaceOrderAsync(Input, User);
            if (!result.Success || result.Order is null)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Gagal memproses pesanan.");
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    TempData["ToastError"] = result.ErrorMessage;
                }
                return Page();
            }

            TempData["ToastSuccess"] = $"Faktur {result.Order.InvoiceNumber} berhasil diterbitkan!";
            return RedirectToPage("/Orders/Invoice", new
            {
                id = result.Order.Id,
                token = result.Order.OrderAccessToken
            });
        }
        catch (InsufficientStockException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            TempData["ToastError"] = ex.Message;
            return Page();
        }
    }

    private async Task LoadPageContextAsync()
    {
        Cart = await _cartService.GetCartSummaryAsync(User);
        var user = await _authService.GetCurrentUserAsync(User);

        IsAdminUser = user?.Role == UserRole.Admin;
        IsApprovedB2B = user is { Role: UserRole.PartnerB2B, IsB2BApproved: true };
        IsPendingB2B = user is { Role: UserRole.PartnerB2B, IsB2BApproved: false };

        RegularShippingCost = _pricing.CalculateShippingCost(ShippingMethod.RegulerKurir, Cart.TotalPairs, IsApprovedB2B);
        CargoShippingCost = _pricing.CalculateShippingCost(ShippingMethod.EkspedisiKargoTruk, Cart.TotalPairs, IsApprovedB2B);
    }
}
