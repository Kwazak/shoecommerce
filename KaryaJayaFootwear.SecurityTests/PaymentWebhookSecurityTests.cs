using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using KaryaJayaFootwear.Data;
using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.SecurityTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KaryaJayaFootwear.SecurityTests;

public class PaymentWebhookSecurityTests : IAsyncLifetime
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
    public async Task Webhook_Rejects_Invalid_Signature_And_Enforces_Idempotency_On_Duplicate_Settlement()
    {
        int initialPhysical;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            initialPhysical = (await db.ProductVariants.AsNoTracking().FirstAsync(v => v.Id == 1)).PhysicalStock;
        }

        // Create MidtransOnline order for 2 pairs of variant 1
        var client = _factory.CreateCookieClient();
        await client.PostAsync("/api/cart/add",
            new StringContent("{\"variantId\":1,\"quantity\":2}", Encoding.UTF8, "application/json"));
        var chkHtml = await client.GetStringAsync("/Checkout");
        var chkToken = CustomWebApplicationFactory.ExtractAntiForgeryToken(chkHtml);

        var chkResp = await client.PostAsync("/Checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.CustomerName"] = "Pembeli Webhook",
            ["Input.CustomerEmail"] = "webhook@contoh.id",
            ["Input.CustomerPhone"] = "08123450000",
            ["Input.ShippingCity"] = "Bandung",
            ["Input.ShippingAddress"] = "Jl. Braga No. 12",
            ["Input.ShippingMethod"] = "RegulerKurir",
            ["Input.PaymentMethod"] = "MidtransOnline",
            ["__RequestVerificationToken"] = chkToken
        }));
        chkResp.EnsureSuccessStatusCode();

        string invoiceNumber;
        string grossAmountStr;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ord = await db.Orders.AsNoTracking().FirstAsync();
            invoiceNumber = ord.InvoiceNumber;
            grossAmountStr = ((long)ord.GrandTotal).ToString() + ".00";
        }

        // 1. Invalid signature -> 400 BadRequest, order remains MenungguPembayaran
        var invalidPayload = JsonSerializer.Serialize(new
        {
            order_id = invoiceNumber,
            status_code = "200",
            gross_amount = grossAmountStr,
            signature_key = "invalid-sha512-signature",
            transaction_status = "settlement",
            fraud_status = "accept"
        });
        var badResp = await client.PostAsync("/api/payments/webhook",
            new StringContent(invalidPayload, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, badResp.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ord = await db.Orders.AsNoTracking().FirstAsync(o => o.InvoiceNumber == invoiceNumber);
            Assert.Equal(OrderStatus.MenungguPembayaran, ord.OrderStatus);
        }

        // 2. Valid SHA-512 signature -> transitions to DibayarSiapDikemas and deducts PhysicalStock once
        var validSig = CustomWebApplicationFactory.ComputeMidtransSignature(invoiceNumber, "200", grossAmountStr);
        var validPayload = JsonSerializer.Serialize(new
        {
            order_id = invoiceNumber,
            status_code = "200",
            gross_amount = grossAmountStr,
            signature_key = validSig,
            transaction_status = "settlement",
            fraud_status = "accept",
            transaction_id = "MID-TEST-001"
        });

        var firstGood = await client.PostAsync("/api/payments/webhook",
            new StringContent(validPayload, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, firstGood.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ord = await db.Orders.AsNoTracking().FirstAsync(o => o.InvoiceNumber == invoiceNumber);
            var v1 = await db.ProductVariants.AsNoTracking().FirstAsync(v => v.Id == 1);
            Assert.Equal(OrderStatus.DibayarSiapDikemas, ord.OrderStatus);
            Assert.Equal(initialPhysical - 2, v1.PhysicalStock);
            Assert.Equal(0, v1.ReservedStock);
        }

        // 3. Duplicate webhook -> idempotent, PhysicalStock is NOT deducted a second time
        var duplicateGood = await client.PostAsync("/api/payments/webhook",
            new StringContent(validPayload, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, duplicateGood.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var v1 = await db.ProductVariants.AsNoTracking().FirstAsync(v => v.Id == 1);
            Assert.Equal(initialPhysical - 2, v1.PhysicalStock);
        }
    }

    [Fact]
    public async Task Localhost_Callback_Simulator_Is_Forbidden_In_Production_Environment()
    {
        await using var prodFactory = new CustomWebApplicationFactory("Production");
        var client = prodFactory.CreateNoRedirectClient();

        await client.PostAsync("/api/cart/add",
            new StringContent("{\"variantId\":1,\"quantity\":1}", Encoding.UTF8, "application/json"));

        var chkGet = await client.GetAsync("/Checkout");
        var chkHtml = await chkGet.Content.ReadAsStringAsync();
        var chkToken = CustomWebApplicationFactory.ExtractAntiForgeryToken(chkHtml);

        var chkPost = await client.PostAsync("/Checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.CustomerName"] = "Prod Guest",
            ["Input.CustomerEmail"] = "prod@contoh.id",
            ["Input.CustomerPhone"] = "0812345678",
            ["Input.ShippingCity"] = "Bandung",
            ["Input.ShippingAddress"] = "Jl. Dago No. 1",
            ["Input.ShippingMethod"] = "RegulerKurir",
            ["Input.PaymentMethod"] = "MidtransOnline",
            ["__RequestVerificationToken"] = chkToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, chkPost.StatusCode);
        var redirectLocation = chkPost.Headers.Location!.ToString();
        var match = Regex.Match(redirectLocation, @"/Orders/Invoice/(\d+)\?token=([a-f0-9]+)");
        Assert.True(match.Success);

        var orderId = match.Groups[1].Value;
        var accessToken = match.Groups[2].Value;

        // In Production, the simulator form is not rendered on Invoice.cshtml; obtain an antiforgery token from /Account/Login
        // to simulate an attacker attempting to POST directly to the SimulateCallback handler.
        var loginResp = await client.GetAsync("/Account/Login");
        var loginHtml = await loginResp.Content.ReadAsStringAsync();
        var simToken = CustomWebApplicationFactory.ExtractAntiForgeryToken(loginHtml);

        var simPost = await client.PostAsync(
            $"/Orders/Invoice/{orderId}?handler=SimulateCallback&token={accessToken}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = simToken }));

        Assert.Equal(HttpStatusCode.Forbidden, simPost.StatusCode);
    }
}
