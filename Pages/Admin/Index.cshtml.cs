using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaryaJayaFootwear.Pages.Admin;

public class IndexModel : PageModel
{
    private readonly AdminAnalyticsService _analytics;

    public IndexModel(AdminAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [BindProperty(SupportsGet = true)]
    public string Range { get; set; } = "30d";

    public AdminAnalyticsDashboardViewModel Dashboard { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Dashboard = await _analytics.GetDashboardAsync(Range);
    }
}
