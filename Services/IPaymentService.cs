using KaryaJayaFootwear.Models;

namespace KaryaJayaFootwear.Services;

public sealed class SnapTransactionResult
{
    public bool Success { get; init; }
    public bool IsPlaceholderMode { get; init; }
    public string? SnapToken { get; init; }
    public string? RedirectUrl { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class MidtransNotificationPayload
{
    public string order_id { get; set; } = string.Empty;
    public string status_code { get; set; } = string.Empty;
    public string gross_amount { get; set; } = string.Empty;
    public string signature_key { get; set; } = string.Empty;
    public string transaction_status { get; set; } = string.Empty;
    public string? fraud_status { get; set; }
    public string? transaction_id { get; set; }
    public string? payment_type { get; set; }
}

public interface IPaymentService
{
    Task<SnapTransactionResult> CreateSnapTransactionAsync(Order order, CancellationToken ct = default);
    bool VerifyWebhookSignature(MidtransNotificationPayload payload);
    Task<(bool Success, string Message)> ProcessWebhookNotificationAsync(MidtransNotificationPayload payload, bool bypassSignatureForLocalSim = false);
}
