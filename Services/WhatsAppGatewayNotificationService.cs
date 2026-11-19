using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KaryaJayaFootwear.Data;
using KaryaJayaFootwear.Models;
using KaryaJayaFootwear.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KaryaJayaFootwear.Services;

public sealed class WhatsAppGatewayNotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WhatsAppGatewayOptions _options;
    private readonly ILogger<WhatsAppGatewayNotificationService> _logger;

    public WhatsAppGatewayNotificationService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<WhatsAppGatewayOptions> options,
        ILogger<WhatsAppGatewayNotificationService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendOrderCreatedAsync(Order order, CancellationToken ct = default)
    {
        var paymentNote = order.PaymentMethod switch
        {
            PaymentMethod.MidtransOnline =>
                $"Silakan selesaikan pembayaran sebesar {FormatHelper.Rupiah(order.GrandTotal)} melalui tautan faktur Anda (batas 24 jam).",
            PaymentMethod.TempoB2B =>
                $"Pesanan Tempo Mitra B2B 14 Hari ({FormatHelper.Rupiah(order.GrandTotal)}) langsung diproses gudang. Jatuh tempo: {(order.TempoDueDate.HasValue ? FormatHelper.WibDateShort(order.TempoDueDate.Value) : "14 hari")}.",
            _ =>
                $"Pesanan COD ({FormatHelper.Rupiah(order.GrandTotal)}) sedang disiapkan untuk pengiriman."
        };

        var msg =
            $"[PABRIK KARYA JAYA 1978] Yth. {order.CustomerName}, faktur {order.InvoiceNumber} ({order.TotalPairs} pasang) telah diterbitkan. {paymentNote}";

        await DispatchAndLogAsync(order.Id, order.CustomerPhone, "PesananDibuat", msg, ct);
    }

    public async Task SendPaymentConfirmedAsync(Order order, CancellationToken ct = default)
    {
        var msg =
            $"[NOTA LUNAS - KARYA JAYA 1978] Pembayaran faktur {order.InvoiceNumber} sebesar {FormatHelper.Rupiah(order.GrandTotal)} telah kami terima LUNAS. Pesanan Anda segera dikemas dari gudang Cibaduyut.";

        await DispatchAndLogAsync(order.Id, order.CustomerPhone, "NotaLunas", msg, ct);
    }

    public async Task SendOrderDispatchedAsync(Order order, CancellationToken ct = default)
    {
        var trackingInfo = !string.IsNullOrWhiteSpace(order.CourierName) && !string.IsNullOrWhiteSpace(order.TrackingNumber)
            ? $" Kurir: {order.CourierName}, No. Resi: {order.TrackingNumber}."
            : string.Empty;

        var msg =
            $"[RESI DIKIRIM - KARYA JAYA 1978] Yth. {order.CustomerName}, pesanan faktur {order.InvoiceNumber} ({order.TotalPairs} pasang) telah diberangkatkan via {order.ShippingMethod.ToIndonesianLabel()} menuju {order.ShippingCity}.{trackingInfo}";

        await DispatchAndLogAsync(order.Id, order.CustomerPhone, "ResiDikirim", msg, ct);
    }

    public async Task<List<NotificationLog>> GetRecentLogsAsync(int take = 40)
    {
        return await _db.NotificationLogs
            .AsNoTracking()
            .Include(n => n.Order)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    private async Task DispatchAndLogAsync(int orderId, string phone, string eventType, string messageBody, CancellationToken ct)
    {
        string deliveryStatus = "Tersimpan (Mode Simulasi Lokal)";

        if (_options.IsConfigured)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("WhatsApp");
                using var req = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                req.Content = new StringContent(
                    JsonSerializer.Serialize(new { phone, message = messageBody, eventType }),
                    Encoding.UTF8,
                    "application/json");

                var resp = await client.SendAsync(req, ct);
                deliveryStatus = resp.IsSuccessStatusCode
                    ? "Terkirim via Gateway"
                    : $"Gagal Gateway HTTP {(int)resp.StatusCode}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WhatsApp gateway error for phone {Phone}", phone);
                deliveryStatus = "Gagal Gateway (Dicatat Lokal)";
            }
        }

        _db.NotificationLogs.Add(new NotificationLog
        {
            OrderId = orderId,
            RecipientPhone = phone,
            EventType = eventType,
            MessageBody = messageBody,
            DeliveryStatus = deliveryStatus,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}
