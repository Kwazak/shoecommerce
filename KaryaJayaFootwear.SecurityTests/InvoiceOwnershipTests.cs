using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using KaryaJayaFootwear.SecurityTests.Infrastructure;
using Xunit;

namespace KaryaJayaFootwear.SecurityTests;

public class InvoiceOwnershipTests : IAsyncLifetime
{
    private CustomWebApplicationFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new CustomWebApplicationFactory();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Authenticated_UserA_Cannot_View_UserB_Invoice_IDOR_Prevented()
    {
        var userAClient = _factory.CreateCookieClient();
        var userBClient = _factory.CreateNoRedirectClient();

        await _factory.RegisterUserAsync(userAClient, "usera@contoh.id", "CustomerB2C", "User A");
        await _factory.RegisterUserAsync(userBClient, "userb@contoh.id", "CustomerB2C", "User B");

        // User A places an order
        await userAClient.PostAsync("/api/cart/add",
            new StringContent("{\"variantId\":1,\"quantity\":1}", Encoding.UTF8, "application/json"));
        var chkHtml = await userAClient.GetStringAsync("/Checkout");
        var chkToken = CustomWebApplicationFactory.ExtractAntiForgeryToken(chkHtml);

        var chkResp = await userAClient.PostAsync("/Checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.CustomerName"] = "User A",
            ["Input.CustomerEmail"] = "usera@contoh.id",
            ["Input.CustomerPhone"] = "08111111111",
            ["Input.ShippingCity"] = "Bandung",
            ["Input.ShippingAddress"] = "Jl. Asia Afrika No. 10",
            ["Input.ShippingMethod"] = "RegulerKurir",
            ["Input.PaymentMethod"] = "COD",
            ["__RequestVerificationToken"] = chkToken
        }));
        chkResp.EnsureSuccessStatusCode();

        var invMatch = Regex.Match(chkResp.RequestMessage!.RequestUri!.ToString(), @"/Orders/Invoice/(\d+)");
        Assert.True(invMatch.Success);
        var orderId = invMatch.Groups[1].Value;

        // User B attempts to access User A's invoice directly
        var snoopResp = await userBClient.GetAsync($"/Orders/Invoice/{orderId}");
        Assert.Equal(HttpStatusCode.Redirect, snoopResp.StatusCode);
        Assert.Contains("/Account/AccessDenied", snoopResp.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Guest_Invoice_Requires_Exact_OrderAccessToken()
    {
        var guestClient = _factory.CreateCookieClient();
        await guestClient.PostAsync("/api/cart/add",
            new StringContent("{\"variantId\":2,\"quantity\":1}", Encoding.UTF8, "application/json"));
        var chkHtml = await guestClient.GetStringAsync("/Checkout");
        var chkToken = CustomWebApplicationFactory.ExtractAntiForgeryToken(chkHtml);

        var chkResp = await guestClient.PostAsync("/Checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.CustomerName"] = "Tamu Eceran",
            ["Input.CustomerEmail"] = "tamu@contoh.id",
            ["Input.CustomerPhone"] = "08222222222",
            ["Input.ShippingCity"] = "Jakarta",
            ["Input.ShippingAddress"] = "Jl. Sudirman No. 5",
            ["Input.ShippingMethod"] = "RegulerKurir",
            ["Input.PaymentMethod"] = "MidtransOnline",
            ["__RequestVerificationToken"] = chkToken
        }));
        chkResp.EnsureSuccessStatusCode();

        var uriMatch = Regex.Match(chkResp.RequestMessage!.RequestUri!.ToString(), @"/Orders/Invoice/(\d+)\?token=([a-f0-9]+)");
        Assert.True(uriMatch.Success);
        var orderId = uriMatch.Groups[1].Value;
        var validToken = uriMatch.Groups[2].Value;

        var anonNoRedirect = _factory.CreateNoRedirectClient();

        // Missing token -> denied
        var noTokenResp = await anonNoRedirect.GetAsync($"/Orders/Invoice/{orderId}");
        Assert.Equal(HttpStatusCode.Redirect, noTokenResp.StatusCode);
        Assert.Contains("/Account/AccessDenied", noTokenResp.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);

        // Tampered token -> denied
        var badTokenResp = await anonNoRedirect.GetAsync($"/Orders/Invoice/{orderId}?token=00000000000000000000000000000000");
        Assert.Equal(HttpStatusCode.Redirect, badTokenResp.StatusCode);
        Assert.Contains("/Account/AccessDenied", badTokenResp.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);

        // Exact valid token -> 200 OK
        var validResp = await anonNoRedirect.GetAsync($"/Orders/Invoice/{orderId}?token={validToken}");
        Assert.Equal(HttpStatusCode.OK, validResp.StatusCode);
    }
}
