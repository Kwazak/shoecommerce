using KaryaJayaFootwear.Data;
using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.SecurityTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KaryaJayaFootwear.SecurityTests;

public class RegistrationSecurityTests : IAsyncLifetime
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
    public async Task OverPosting_Admin_Role_Or_IsB2BApproved_At_Registration_Is_Ignored()
    {
        var client = _factory.CreateCookieClient();
        var regHtml = await client.GetStringAsync("/Account/Register?type=PartnerB2B");
        var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(regHtml);

        var email = $"hacker_{Guid.NewGuid():N}@contoh.id";
        var resp = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.AccountType"] = "PartnerB2B",
            ["Input.Role"] = "Admin",
            ["Role"] = "Admin",
            ["Input.IsB2BApproved"] = "true",
            ["IsB2BApproved"] = "true",
            ["Input.FullName"] = "Attacker Shop",
            ["Input.PhoneNumber"] = "081999999999",
            ["Input.Email"] = email,
            ["Input.ShopName"] = "Toko Curang",
            ["Input.City"] = "Surabaya",
            ["Input.ShopAddress"] = "Jl. Pahlawan No. 9",
            ["Input.Password"] = "Rahasia123!",
            ["Input.ConfirmPassword"] = "Rahasia123!",
            ["__RequestVerificationToken"] = token
        }));
        resp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email);

        Assert.NotNull(user);
        Assert.Equal(UserRole.PartnerB2B, user!.Role);
        Assert.False(user.IsB2BApproved);
    }
}
