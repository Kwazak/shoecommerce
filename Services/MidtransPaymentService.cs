using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KaryaJayaFootwear.Data;
using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace KaryaJayaFootwear.Services;

public sealed class MidtransPaymentService : IPaymentService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MidtransOptions _options;
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly HybridCache _cache;
    private readonly ILogger<MidtransPaymentService> _logger;

    public MidtransPaymentService(
        IHttpClientFactory httpClientFactory,
        IOptions<MidtransOptions> options,
        AppDbContext db,
        INotificationService notifications,
        HybridCache cache,
        ILogger<MidtransPaymentService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _db = db;
        _notifications = notifications;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SnapTransactionResult> CreateSnapTransactionAsync(Order order, CancellationToken ct = default)
    {
        if (!_options.HasValidKeys)
        {
            return new SnapTransactionResult
            {
                Success = false,
                IsPlaceholderMode = true,
                ErrorMessage = "Kunci Sandbox Midtrans pada appsettings.json masih berupa placeholder. Gunakan tombol 'Simulasi Callback Lunas Midtrans (Mode Uji Lokal)' atau isi ServerKey/ClientKey Sandbox Midtrans Anda."
            };
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Midtrans");
            client.BaseAddress = new Uri(_options.SnapApiBaseUrl);

            var authBytes = Encoding.UTF8.GetBytes($"{_options.ServerKey}:");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var itemDetails = order.Items.Select(i => new
            {
                id = i.ProductSku,
                price = (long)i.UnitAppliedPrice,
                quantity = i.Quantity,
                name = $"{i.ProductName} (Uk.{i.Size})".Length > 50
                    ? $"{i.ProductName} (Uk.{i.Size})"[..50]
                    : $"{i.ProductName} (Uk.{i.Size})"
            }).ToList();

            if (order.ShippingCost > 0)
            {
                itemDetails.Add(new
                {
                    id = "ONGKIR-PABRIK",
                    price = (long)order.ShippingCost,
                    quantity = 1,
                    name = "Ongkos Kirim Pabrik"
                });
            }

            long calculatedGross = itemDetails.Sum(x => x.price * x.quantity);
            long orderGross = (long)order.GrandTotal;
            if (calculatedGross != orderGross)
            {
                long diff = orderGross - calculatedGross;
                itemDetails.Add(new
                {
                    id = "PENYESUAIAN",
                    price = diff,
                    quantity = 1,
                    name = "Penyesuaian Pembulatan Faktur"
                });
            }

            var requestBody = new
            {
                transaction_details = new
                {
                    order_id = order.InvoiceNumber,
                    gross_amount = orderGross
                },
                customer_details = new
                {
                    first_name = order.CustomerName,
                    email = order.CustomerEmail,
                    phone = order.CustomerPhone,
                    shipping_address = new
                    {
                        first_name = order.CustomerName,
                        phone = order.CustomerPhone,
                        address = order.ShippingAddress,
                        city = order.ShippingCity,
                        country_code = "IDN"
                    }
                },
                item_details = itemDetails
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/snap/v1/transactions", content, ct);
            var responseJson = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Midtrans Snap returned {StatusCode}: {Body}", response.StatusCode, responseJson);
                return new SnapTransactionResult
                {
                    Success = false,
                    ErrorMessage = $"Midtrans Snap merespons {(int)response.StatusCode}. Periksa ServerKey Sandbox Anda."
                };
            }

            using var doc = JsonDocument.Parse(responseJson);
            var token = doc.RootElement.TryGetProperty("token", out var tProp) ? tProp.GetString() : null;
            var redirectUrl = doc.RootElement.TryGetProperty("redirect_url", out var rProp) ? rProp.GetString() : null;

            return new SnapTransactionResult
            {
                Success = !string.IsNullOrWhiteSpace(token),
                SnapToken = token,
                RedirectUrl = redirectUrl
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal menghubungi Midtrans Snap API untuk faktur {Invoice}", order.InvoiceNumber);
            return new SnapTransactionResult
            {
                Success = false,
                ErrorMessage = "Tidak dapat terhubung ke server Sandbox Midtrans saat ini. Anda dapat memakai Simulasi Callback Lokal untuk pengujian."
            };
        }
    }

    public bool VerifyWebhookSignature(MidtransNotificationPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.order_id) ||
            string.IsNullOrWhiteSpace(payload.status_code) ||
            string.IsNullOrWhiteSpace(payload.gross_amount) ||
            string.IsNullOrWhiteSpace(payload.signature_key))
        {
            return false;
        }

        string raw = $"{payload.order_id}{payload.status_code}{payload.gross_amount}{_options.ServerKey}";
        byte[] hashBytes = SHA512.HashData(Encoding.UTF8.GetBytes(raw));
        string computed = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(payload.signature_key.Trim().ToLowerInvariant()));
    }

    public async Task<(bool Success, string Message)> ProcessWebhookNotificationAsync(
        MidtransNotificationPayload payload,
        bool bypassSignatureForLocalSim = false)
    {
        if (!bypassSignatureForLocalSim && !VerifyWebhookSignature(payload))
        {
            return (false, "Signature SHA-512 Midtrans tidak valid.");
        }

        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.InvoiceNumber == payload.order_id);

        if (order is null)
        {
            return (false, $"Faktur {payload.order_id} tidak ditemukan.");
        }

        var status = payload.transaction_status.Trim().ToLowerInvariant();
        var fraud = payload.fraud_status?.Trim().ToLowerInvariant();

        if (status == "settlement" || (status == "capture" && (string.IsNullOrEmpty(fraud) || fraud == "accept")))
        {
            if (order.OrderStatus == OrderStatus.MenungguPembayaran || order.PaymentMethod == PaymentMethod.TempoB2B)
            {
                await using var tx = await _db.Database.BeginTransactionAsync();
                try
                {
                    if (order.OrderStatus == OrderStatus.MenungguPembayaran)
                    {
                        var variantIds = order.Items.Select(i => i.ProductVariantId).Distinct().ToList();
                        var variants = await _db.ProductVariants
                            .Where(v => variantIds.Contains(v.Id))
                            .ToDictionaryAsync(v => v.Id);

                        foreach (var item in order.Items)
                        {
                            if (variants.TryGetValue(item.ProductVariantId, out var variant))
                            {
                                variant.PhysicalStock = Math.Max(0, variant.PhysicalStock - item.Quantity);
                                variant.ReservedStock = Math.Max(0, variant.ReservedStock - item.Quantity);
                                variant.ConcurrencyStamp = Guid.NewGuid();
                            }
                        }

                        order.OrderStatus = OrderStatus.DibayarSiapDikemas;
                    }

                    order.PaidAt ??= DateTime.UtcNow;
                    order.IsTempoNearDue = false;
                    order.MidtransTransactionId = string.IsNullOrWhiteSpace(payload.transaction_id)
                        ? $"SIM-{DateTime.UtcNow:yyyyMMddHHmmss}"
                        : payload.transaction_id;

                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                    await _cache.RemoveByTagAsync("catalog");
                    await _notifications.SendPaymentConfirmedAsync(order);
                }
                catch (DbUpdateConcurrencyException)
                {
                    await tx.RollbackAsync();
                    return (false, "Terjadi bentrok pembaruan stok saat konfirmasi pembayaran. Silakan ulangi.");
                }
            }

            return (true, $"Pembayaran faktur {order.InvoiceNumber} terverifikasi LUNAS. Status: {order.OrderStatus.ToIndonesianLabel()}.");
        }

        if (status is "cancel" or "deny" or "expire")
        {
            if (order.OrderStatus == OrderStatus.MenungguPembayaran && !order.PaidAt.HasValue)
            {
                await using var tx = await _db.Database.BeginTransactionAsync();
                try
                {
                    order.OrderStatus = status == "expire" ? OrderStatus.Kedaluwarsa : OrderStatus.Dibatalkan;
                    var variantIds = order.Items.Select(i => i.ProductVariantId).Distinct().ToList();
                    var variants = await _db.ProductVariants
                        .Where(v => variantIds.Contains(v.Id))
                        .ToDictionaryAsync(v => v.Id);

                    foreach (var item in order.Items)
                    {
                        if (variants.TryGetValue(item.ProductVariantId, out var variant))
                        {
                            variant.ReservedStock = Math.Max(0, variant.ReservedStock - item.Quantity);
                            variant.ConcurrencyStamp = Guid.NewGuid();
                        }
                    }

                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                    await _cache.RemoveByTagAsync("catalog");
                }
                catch (DbUpdateConcurrencyException)
                {
                    await tx.RollbackAsync();
                    return (false, "Bentrok pembaruan stok saat pembatalan transaksi.");
                }
            }

            return (true, $"Transaksi faktur {order.InvoiceNumber} dibatalkan/kedaluwarsa.");
        }

        return (true, $"Status notifikasi '{status}' dicatat.");
    }
}
