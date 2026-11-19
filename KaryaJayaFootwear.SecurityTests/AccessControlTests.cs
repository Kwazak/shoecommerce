using System.Net;
using System.Text;
using KaryaJayaFootwear.SecurityTests.Infrastructure;
using Xunit;

namespace KaryaJayaFootwear.SecurityTests;

public class AccessControlTests : IAsyncLifetime
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

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Index")]
    [InlineData("/Admin/Orders")]
    [InlineData("/Admin/Products")]
    [InlineData("/Admin/Partners")]
    [InlineData("/Admin/Discounts")]
    [InlineData("/Admin/WarehouseScan")]
    public async Task Anonymous_Requests_To_Admin_Routes_Redirect_To_Login(string path)
    {
        var client = _factory.CreateNoRedirectClient();
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/Admin/Index")]
    [InlineData("/Admin/Orders")]
    [InlineData("/Admin/Products")]
    [InlineData("/Admin/Partners")]
    [InlineData("/Admin/Discounts")]
    public async Task CustomerB2C_Requests_To_Admin_Routes_Are_Denied(string path)
    {
        var client = _factory.CreateNoRedirectClient();
        await _factory.RegisterUserAsync(client, $"b2c_{Guid.NewGuid():N}@contoh.id", "CustomerB2C");

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Warehouse_Api_Returns_401_Without_Or_With_Invalid_Key_And_200_With_Valid_Key()
    {
        // Create an order first via guest checkout so we have a real invoice number
        var guestClient = _factory.CreateCookieClient();
        var addRes = await guestClient.PostAsync("/api/cart/add",
            new StringContent("{\"variantId\":1,\"quantity\":1}", Encoding.UTF8, "application/json"));
        addRes.EnsureSuccessStatusCode();

        var chkHtml = await guestClient.GetStringAsync("/Checkout");
        var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(chkHtml);
        var chkResp = await guestClient.PostAsync("/Checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.CustomerName"] = "Pembeli Gudang",
            ["Input.CustomerEmail"] = "gudang@contoh.id",
            ["Input.CustomerPhone"] = "081200001111",
            ["Input.ShippingCity"] = "Bandung",
            ["Input.ShippingAddress"] = "Jl. Cibaduyut No. 1",
            ["Input.ShippingMethod"] = "RegulerKurir",
            ["Input.PaymentMethod"] = "COD",
            ["__RequestVerificationToken"] = token
        }));
        chkResp.EnsureSuccessStatusCode();

        var invMatch = System.Text.RegularExpressions.Regex.Match(
            await chkResp.Content.ReadAsStringAsync(), @"FKJ-\d{8}-\d{4}");
        Assert.True(invMatch.Success);
        var invoiceNumber = invMatch.Value;

        var anonClient = _factory.CreateNoRedirectClient();

        // Missing API key -> 401
        var missingKeyResp = await anonClient.GetAsync($"/api/v1/warehouse/orders/{invoiceNumber}");
        Assert.Equal(HttpStatusCode.Unauthorized, missingKeyResp.StatusCode);

        // Wrong API key -> 401
        using var wrongReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/warehouse/orders/{invoiceNumber}");
        wrongReq.Headers.Add("X-Factory-Api-Key", "WRONG-KEY-999");
        var wrongKeyResp = await anonClient.SendAsync(wrongReq);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongKeyResp.StatusCode);

        // Valid API key -> 200
        using var validReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/warehouse/orders/{invoiceNumber}");
        validReq.Headers.Add("X-Factory-Api-Key", CustomWebApplicationFactory.TestWarehouseApiKey);
        var validResp = await anonClient.SendAsync(validReq);
        Assert.Equal(HttpStatusCode.OK, validResp.StatusCode);
    }
}
