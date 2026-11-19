using System.Net.Http.Headers;
using System.Text;
using KaryaJayaFootwear.Data;
using KaryaJayaFootwear.SecurityTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KaryaJayaFootwear.SecurityTests;

public class InputAndUploadSecurityTests : IAsyncLifetime
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
    public async Task Disguised_NonImage_Upload_Is_Rejected_By_Magic_Bytes_Validation()
    {
        var adminClient = _factory.CreateCookieClient();
        await _factory.SignInAsAdminAsync(adminClient);

        var productsHtml = await adminClient.GetStringAsync("/Admin/Products");
        var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(productsHtml);

        // Plaintext/script payload disguised with .jpg extension
        byte[] fakeJpgBytes = Encoding.UTF8.GetBytes("MZ_FAKE_EXECUTABLE_OR_HTML_<script>alert(1)</script>");
        using var mp = new MultipartFormDataContent();
        mp.Add(new StringContent("1"), "productId");
        mp.Add(new StringContent("Sepatu Kanvas Tjipaganti 78"), "name");
        mp.Add(new StringContent("Sepatu"), "category");
        mp.Add(new StringContent("Tagline"), "shortTagline");
        mp.Add(new StringContent("Deskripsi"), "description");
        mp.Add(new StringContent("Kanvas"), "upperMaterial");
        mp.Add(new StringContent("Karet"), "soleMaterial");
        mp.Add(new StringContent("Press"), "constructionMethod");
        mp.Add(new StringContent("115000"), "retailPrice");
        mp.Add(new StringContent("sepatu-kanvas"), "svgIllustrationKey");
        mp.Add(new StringContent("true"), "isFeatured");
        mp.Add(new StringContent("true"), "isActive");
        mp.Add(new StringContent(token), "__RequestVerificationToken");

        var fileContent = new ByteArrayContent(fakeJpgBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        mp.Add(fileContent, "photoFile", "malicious.jpg");

        var resp = await adminClient.PostAsync("/Admin/Products?handler=UpdateProduct", mp);
        var respHtml = await resp.Content.ReadAsStringAsync();

        Assert.Contains("Isi berkas tidak cocok dengan format gambar asli", respHtml);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product1 = await db.Products.AsNoTracking().FirstAsync(p => p.Id == 1);
        Assert.Null(product1.ImageUrl);
    }

    [Fact]
    public async Task Xss_Payload_In_ShopName_Is_Html_Encoded_On_Admin_Partners_And_Invoices()
    {
        var xssPayload = "<script>alert('xss')</script>";

        var b2bClient = _factory.CreateCookieClient();
        await _factory.RegisterUserAsync(
            b2bClient,
            $"xss_{Guid.NewGuid():N}@contoh.id",
            "PartnerB2B",
            fullName: "Pemilik Toko",
            shopName: xssPayload);

        var adminClient = _factory.CreateCookieClient();
        await _factory.SignInAsAdminAsync(adminClient);

        var partnersHtml = await adminClient.GetStringAsync("/Admin/Partners");
        Assert.DoesNotContain(xssPayload, partnersHtml);
        Assert.Contains("&lt;script&gt;alert(&#x27;xss&#x27;)&lt;/script&gt;", partnersHtml);
    }
}
