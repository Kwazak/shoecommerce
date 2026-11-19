using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaryaJayaFootwear.Pages.Admin;

public class OrdersModel : PageModel
{
    private readonly OrderFulfillmentService _fulfillment;
    private readonly INotificationService _notifications;

    public OrdersModel(OrderFulfillmentService fulfillment, INotificationService notifications)
    {
        _fulfillment = fulfillment;
        _notifications = notifications;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public OrderStatus? StatusFilter { get; set; }

    public List<Order> Orders { get; private set; } = new();
    public List<NotificationLog> WhatsAppLogs { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Orders = await _fulfillment.GetOrdersForAdminAsync(Search, StatusFilter);
        WhatsAppLogs = await _notifications.GetRecentLogsAsync(40);
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(int orderId, OrderStatus newStatus)
    {
        var (success, message) = await _fulfillment.UpdateOrderStatusByAdminAsync(orderId, newStatus);
        TempData[success ? "ToastSuccess" : "ToastError"] = message;
        return RedirectToPage(new { Search, StatusFilter });
    }

    public async Task<IActionResult> OnPostMarkPaidAsync(int orderId)
    {
        var ok = await _fulfillment.MarkTempoOrderPaidAsync(orderId);
        TempData[ok ? "ToastSuccess" : "ToastError"] = ok
            ? "Faktur ditandai LUNAS di buku kas."
            : "Faktur tidak ditemukan.";
        return RedirectToPage(new { Search, StatusFilter });
    }

    public async Task<IActionResult> OnPostRunLifecycleCheckAsync()
    {
        var res = await _fulfillment.RunOrderLifecycleCheckAsync();
        TempData["ToastInfo"] =
            $"Pengecekan selesai: {res.ExpiredOrdersCount} faktur kedaluwarsa ({res.ReleasedPairsCount} pasang dilepaskan), {res.FlaggedTempoNearDueCount} faktur Tempo B2B ditandai JATUH TEMPO.";
        return RedirectToPage(new { Search, StatusFilter });
    }
}
