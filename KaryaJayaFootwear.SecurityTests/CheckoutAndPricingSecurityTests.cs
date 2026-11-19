using System.Net;
using System.Text;
using System.Text.Json;
using KaryaJayaFootwear.Data;
using KaryaJayaFootwear.SecurityTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KaryaJayaFootwear.SecurityTests;

public class CheckoutAndPricingSecurityTests : IAsyncLifetime
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
    public async Task Unapproved_PartnerB2B_Cannot_Use_TempoB2B_Or_Claim_Wholesale_Discounts()
    {
        var b2bClient = _factory.CreateCookieClient();
        await _factory.RegisterUserAsync(b2bClient, $"pendingb2b_{Guid.NewGuid():N}@contoh.id", "PartnerB2B");

        // Add 12 pairs (1 Lusin) while IsB2BApproved == false
        var addRes = await b2bClient.PostAsync("/api/cart/add",
            new StringContent("{\"variantId\":1,\"quantity\":12}", Encoding.UTF8, "application/json"));
        addRes.EnsureSuccessStatusCode();

        // Cart must NOT apply wholesale pricing
        var cartJson = await b2bClient.GetStringAsync("/api/cart");
        using (var doc = JsonDocument.Parse(cartJson))
        {
            Assert.False(doc.RootElement.GetProperty("isWholesalePricingActive").GetBoolean());
            Assert.Equal(0m, doc.RootElement.GetProperty("discountPercent").GetDecimal());
        }

        // Attempting to POST PaymentMethod=TempoB2B must be blocked by server-side guard
        var chkHtml = await b2bClient.GetStringAsync("/Checkout");
        var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(chkHtml);

        var chkResp = await b2bClient.PostAsync("/Checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.CustomerName"] = "Mitra Pending",
            ["Input.CustomerEmail"] = "pendingb2b@contoh.id",
            ["Input.CustomerPhone"] = "08123456789",
            ["Input.ShippingCity"] = "Bandung",
            ["Input.ShippingAddress"] = "Jl. Pasar Baru No. 2",
            ["Input.ShippingMethod"] = "RegulerKurir",
            ["Input.PaymentMethod"] = "TempoB2B",
            ["__RequestVerificationToken"] = token
        }));

        var body = await chkResp.Content.ReadAsStringAsync();
        Assert.Contains("Tempo Mitra B2B (14 Hari) hanya tersedia bagi Mitra Toko B2B yang telah disetujui", body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.Orders.CountAsync());
    }

    [Fact]
    public async Task Negative_And_Zero_Quantities_Are_Rejected_Without_Corrupting_Cart_Or_Stock()
    {
        var client = _factory.CreateCookieClient();

        // Single add with 0 and -3 -> 400 BadRequest
        var zeroAdd = await client.PostAsync("/api/cart/add",
            new StringContent("{\"variantId\":1,\"quantity\":0}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, zeroAdd.StatusCode);

        var negAdd = await client.PostAsync("/api/cart/add",
            new StringContent("{\"variantId\":1,\"quantity\":-3}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, negAdd.StatusCode);

        // Add 2 valid pairs, then attempt update with 0 or -2 -> 400 BadRequest
        var okAdd = await client.PostAsync("/api/cart/add",
            new StringContent("{\"variantId\":1,\"quantity\":2}", Encoding.UTF8, "application/json"));
        okAdd.EnsureSuccessStatusCode();

        var zeroUpdate = await client.PostAsync("/api/cart/update",
            new StringContent("{\"variantId\":1,\"quantity\":0}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, zeroUpdate.StatusCode);

        var negUpdate = await client.PostAsync("/api/cart/update",
            new StringContent("{\"variantId\":1,\"quantity\":-2}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, negUpdate.StatusCode);

        // B2B Matrix batch submission with negative quantity -> rejected, cart remains at 2 pairs
        var detailsHtml = await client.GetStringAsync("/Products/Details/1?SimulateB2B=true");
        var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(detailsHtml);
        var batchResp = await client.PostAsync("/Products/Details/1?handler=AddBatch", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["variantQuantities[1]"] = "-3",
            ["variantQuantities[2]"] = "1",
            ["simulateB2B"] = "true",
            ["__RequestVerificationToken"] = token
        }));
        batchResp.EnsureSuccessStatusCode();

        var cartJson = await client.GetStringAsync("/api/cart");
        using var doc = JsonDocument.Parse(cartJson);
        Assert.Equal(2, doc.RootElement.GetProperty("totalPairs").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var v1 = await db.ProductVariants.AsNoTracking().FirstAsync(v => v.Id == 1);
        Assert.Equal(0, v1.ReservedStock);
    }
}
