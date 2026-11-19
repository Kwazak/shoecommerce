namespace KaryaJayaFootwear.Options;

public sealed class MidtransOptions
{
    public const string SectionName = "Midtrans";

    public string ServerKey { get; set; } = string.Empty;
    public string ClientKey { get; set; } = string.Empty;
    public bool IsProduction { get; set; }

    public string SnapApiBaseUrl => IsProduction
        ? "https://app.midtrans.com"
        : "https://app.sandbox.midtrans.com";

    public string SnapJsUrl => IsProduction
        ? "https://app.midtrans.com/snap/snap.js"
        : "https://app.sandbox.midtrans.com/snap/snap.js";

    public bool HasValidKeys =>
        !string.IsNullOrWhiteSpace(ServerKey) &&
        !string.IsNullOrWhiteSpace(ClientKey) &&
        !ServerKey.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase) &&
        !ClientKey.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);
}
