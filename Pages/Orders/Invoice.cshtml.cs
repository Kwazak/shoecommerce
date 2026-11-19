using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Options;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace KaryaJayaFootwear.Pages.Orders;

public class InvoiceModel : PageModel
{
    private readonly OrderFulfillmentService _fulfillment;
    private readonly MidtransOptions _midtrans;
    private readonly IWebHostEnvironment _env;

    public InvoiceModel(
        OrderFulfillmentService fulfillment,
        IOptions<MidtransOptions> midtrans,
        IWebHostEnvironment env)
    {
        _fulfillment = fulfillment;
        _midtrans = midtrans.Value;
        _env = env;
    }

    public Order Order { get; private set; } = null!;
    public string? AccessToken { get; private set; }
    public string MidtransClientKey => _midtrans.ClientKey;
    public string MidtransSnapJsUrl => _midtrans.SnapJsUrl;
    public bool HasValidMidtransKeys => _midtrans.HasValidKeys;
    public bool IsDevelopment => _env.IsDevelopment();
    public string? SnapErrorNotice { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, string? token)
    {
        var order = await _fulfillment.GetOrderForInvoiceAsync(id, User, token);
        if (order is null)
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        Order = order;
        AccessToken = token;

        if (order.PaymentMethod == PaymentMethod.MidtransOnline &&
            order.OrderStatus == OrderStatus.MenungguPembayaran &&
            string.IsNullOrWhiteSpace(order.MidtransSnapToken))
        {
            var snapRes = await _fulfillment.EnsureSnapTokenForOrderAsync(order);
            if (!snapRes.Success)
            {
                SnapErrorNotice = snapRes.ErrorMessage;
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSimulateCallbackAsync(int id, string? token)
    {
        var order = await _fulfillment.GetOrderForInvoiceAsync(id, User, token);
        if (order is null)
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        var (success, message) = await _fulfillment.SimulateLocalMidtransSettlementAsync(order);
        TempData[success ? "ToastSuccess" : "ToastError"] = message;
        return RedirectToPage(new { id, token });
    }
}
