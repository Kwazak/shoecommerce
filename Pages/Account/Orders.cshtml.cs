using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaryaJayaFootwear.Pages.Account;

public class OrdersModel : PageModel
{
    private readonly AuthService _auth;
    private readonly OrderFulfillmentService _fulfillment;

    public OrdersModel(AuthService auth, OrderFulfillmentService fulfillment)
    {
        _auth = auth;
        _fulfillment = fulfillment;
    }

    public User CurrentUser { get; private set; } = null!;
    public List<Order> Orders { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var user = await _auth.GetCurrentUserAsync(User);
        if (user is null) return;

        CurrentUser = user;
        Orders = await _fulfillment.GetOrdersForUserAsync(user.Id);
    }
}
