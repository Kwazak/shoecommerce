using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaryaJayaFootwear.Models;

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public int ProductVariantId { get; set; }

    [Required, MaxLength(140)]
    public string ProductName { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string ProductSku { get; set; } = string.Empty;

    public FootwearCategory Category { get; set; }

    public int Size { get; set; }

    [Required, MaxLength(60)]
    public string ColorName { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string SvgIllustrationKey { get; set; } = "sepatu-kanvas";

    [Required, MaxLength(12)]
    public string ColorHex { get; set; } = "#4A2E1B";

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,0)")]
    public decimal UnitRetailPrice { get; set; }

    [Column(TypeName = "decimal(18,0)")]
    public decimal UnitAppliedPrice { get; set; }

    [Column(TypeName = "decimal(18,0)")]
    public decimal LineTotal { get; set; }
}
