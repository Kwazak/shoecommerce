using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaryaJayaFootwear.Models;

public class Order
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string OrderAccessToken { get; set; } = string.Empty;

    public int? UserId { get; set; }
    public User? User { get; set; }

    [Required, MaxLength(120)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string CustomerPhone { get; set; } = string.Empty;

    [MaxLength(160)]
    public string? ShopName { get; set; }

    [Required, MaxLength(100)]
    public string ShippingCity { get; set; } = string.Empty;

    [Required, MaxLength(350)]
    public string ShippingAddress { get; set; } = string.Empty;

    public ShippingMethod ShippingMethod { get; set; } = ShippingMethod.RegulerKurir;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.MidtransOnline;

    public OrderStatus OrderStatus { get; set; } = OrderStatus.MenungguPembayaran;

    public bool IsWholesaleOrder { get; set; }

    public int TotalPairs { get; set; }

    [Column(TypeName = "decimal(18,0)")]
    public decimal GrossSubtotal { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal DiscountPercentApplied { get; set; }

    [Column(TypeName = "decimal(18,0)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,0)")]
    public decimal ShippingCost { get; set; }

    [Column(TypeName = "decimal(18,0)")]
    public decimal GrandTotal { get; set; }

    [MaxLength(200)]
    public string? MidtransSnapToken { get; set; }

    [MaxLength(400)]
    public string? MidtransRedirectUrl { get; set; }

    [MaxLength(120)]
    public string? MidtransTransactionId { get; set; }

    [MaxLength(80)]
    public string? CourierName { get; set; }

    [MaxLength(100)]
    public string? TrackingNumber { get; set; }

    public DateTime? DispatchedAt { get; set; }

    public DateTime? TempoDueDate { get; set; }

    public bool IsTempoNearDue { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    [NotMapped]
    public bool IsPaid => PaidAt.HasValue || (OrderStatus is OrderStatus.DibayarSiapDikemas or OrderStatus.DijahitDikemas or OrderStatus.Dikirim or OrderStatus.Selesai
        && PaymentMethod != PaymentMethod.TempoB2B);
}
