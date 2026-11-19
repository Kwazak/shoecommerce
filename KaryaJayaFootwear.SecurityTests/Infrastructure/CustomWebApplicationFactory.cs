using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KaryaJayaFootwear.SecurityTests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<global::Program>
{
    public const string TestMidtransServerKey = "SB-Mid-server-TEST-SECRET-KEY";
    public const string TestWarehouseApiKey = "TEST-GUDANG-KEY-1978";

    private readonly string _environmentName;
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"kj_sec_{Guid.NewGuid():N}.db");

    public CustomWebApplicationFactory(string environmentName = "Testing")
    {
        _environmentName = environmentName;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Midtrans:ServerKey", TestMidtransServerKey);
        builder.UseSetting("Midtrans:ClientKey", "SB-Mid-client-TEST-KEY");
        builder.UseSetting("Midtrans:IsProduction", "false");
        builder.UseSetting("Warehouse:ApiKey", TestWarehouseApiKey);
    }

    public HttpClient CreateNoRedirectClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    public HttpClient CreateCookieClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = true,
        HandleCookies = true
    });

    public static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
        if (!match.Success)
        {
            match = Regex.Match(html, @"value=""([^""]+)""\s+name=""__RequestVerificationToken""");
        }
        if (!match.Success)
        {
            throw new InvalidOperationException("Antiforgery token not found in HTML response.");
        }
        return match.Groups[1].Value;
    }

    public static string ComputeMidtransSignature(string orderId, string statusCode, string grossAmount)
    {
        var raw = $"{orderId}{statusCode}{grossAmount}{TestMidtransServerKey}";
        var bytes = SHA512.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public async Task SignInAsAdminAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/Account/Login");
        var token = ExtractAntiForgeryToken(html);
        var resp = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = "admin@pabrik.id",
            ["Input.Password"] = "Pabrik123!",
            ["__RequestVerificationToken"] = token
        }));
        resp.EnsureSuccessStatusCode();
    }

    public async Task RegisterUserAsync(
        HttpClient client,
        string email,
        string accountType,
        string fullName = "Pengguna Uji",
        string? shopName = null)
    {
        var html = await client.GetStringAsync("/Account/Register");
        var token = ExtractAntiForgeryToken(html);
        var form = new Dictionary<string, string>
        {
            ["Input.AccountType"] = accountType,
            ["Input.FullName"] = fullName,
            ["Input.PhoneNumber"] = "081234567890",
            ["Input.Email"] = email,
            ["Input.Password"] = "Rahasia123!",
            ["Input.ConfirmPassword"] = "Rahasia123!",
            ["__RequestVerificationToken"] = token
        };

        if (accountType == "PartnerB2B")
        {
            form["Input.ShopName"] = shopName ?? "Toko Sepatu Uji";
            form["Input.City"] = "Bandung";
            form["Input.ShopAddress"] = "Jl. Pasar Baru No. 1";
        }

        var resp = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(form));
        if (!resp.IsSuccessStatusCode && resp.StatusCode != System.Net.HttpStatusCode.Redirect)
        {
            resp.EnsureSuccessStatusCode();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _dbPath + suffix;
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }
        }
    }
}
