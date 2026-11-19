using System.Security.Claims;
using KaryaJayaFootwear.Data;
using KaryaJayaFootwear.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KaryaJayaFootwear.Services;

public sealed class AuthResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public User? User { get; init; }
}

public sealed class RegisterRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public UserRole AccountType { get; set; } = UserRole.CustomerB2C;
    public string? ShopName { get; set; }
    public string? City { get; set; }
    public string? ShopAddress { get; set; }
}

public sealed class AuthService
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<User> _hasher = new();

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest req)
    {
        var normalizedEmail = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
        {
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "Alamat email sudah terdaftar. Silakan masuk menggunakan email tersebut."
            };
        }

        // Prevent privilege escalation: registration only permits CustomerB2C or PartnerB2B.
        var assignedRole = req.AccountType == UserRole.PartnerB2B
            ? UserRole.PartnerB2B
            : UserRole.CustomerB2C;

        if (assignedRole == UserRole.PartnerB2B)
        {
            if (string.IsNullOrWhiteSpace(req.ShopName) ||
                string.IsNullOrWhiteSpace(req.City) ||
                string.IsNullOrWhiteSpace(req.ShopAddress))
            {
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = "Mitra Toko / Grosir wajib mengisi Nama Toko, Kota/Kabupaten, dan Alamat Lengkap Toko."
                };
            }
        }

        var user = new User
        {
            FullName = req.FullName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = req.PhoneNumber.Trim(),
            Role = assignedRole,
            IsB2BApproved = false,
            ShopName = assignedRole == UserRole.PartnerB2B ? req.ShopName!.Trim() : null,
            City = assignedRole == UserRole.PartnerB2B ? req.City!.Trim() : req.City?.Trim(),
            ShopAddress = assignedRole == UserRole.PartnerB2B ? req.ShopAddress!.Trim() : req.ShopAddress?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, req.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return new AuthResult { Success = true, User = user };
    }

    public async Task<AuthResult> ValidateLoginAsync(string email, string password)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
        if (user is null)
        {
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "Email atau kata sandi tidak sesuai dengan catatan pabrik."
            };
        }

        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verify == PasswordVerificationResult.Failed)
        {
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "Email atau kata sandi tidak sesuai dengan catatan pabrik."
            };
        }

        return new AuthResult { Success = true, User = user };
    }

    public async Task SignInUserAsync(HttpContext httpContext, User user, bool rememberMe = true)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("IsB2BApproved", user.IsB2BApproved ? "true" : "false")
        };

        if (!string.IsNullOrWhiteSpace(user.ShopName))
        {
            claims.Add(new Claim("ShopName", user.ShopName));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
            });
    }

    public async Task<User?> GetCurrentUserAsync(ClaimsPrincipal principal)
    {
        var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idClaim, out var userId))
        {
            return null;
        }

        return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<bool> UpdateProfileAsync(int userId, string fullName, string phoneNumber, string? city, string? address)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return false;

        user.FullName = fullName.Trim();
        user.PhoneNumber = phoneNumber.Trim();
        if (!string.IsNullOrWhiteSpace(city)) user.City = city.Trim();
        if (!string.IsNullOrWhiteSpace(address)) user.ShopAddress = address.Trim();

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<User>> GetAllB2BPartnersAsync()
    {
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.PartnerB2B)
            .OrderBy(u => u.IsB2BApproved)
            .ThenByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> SetB2BApprovalAsync(int partnerUserId, bool approved)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == partnerUserId && u.Role == UserRole.PartnerB2B);
        if (user is null) return false;

        user.IsB2BApproved = approved;
        await _db.SaveChangesAsync();
        return true;
    }
}
