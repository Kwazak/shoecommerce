namespace KaryaJayaFootwear.Options;

public sealed class WhatsAppGatewayOptions
{
    public const string SectionName = "WhatsAppGateway";

    public string ApiUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey);
}
