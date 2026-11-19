using System.ComponentModel.DataAnnotations;

namespace KaryaJayaFootwear.Models;

public class NotificationLog
{
    public int Id { get; set; }

    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    [Required, MaxLength(40)]
    public string RecipientPhone { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string EventType { get; set; } = string.Empty;

    [Required, MaxLength(1500)]
    public string MessageBody { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string DeliveryStatus { get; set; } = "Tersimpan (Mode Simulasi Lokal)";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
