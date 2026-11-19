using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaryaJayaFootwear.Models;

public class ProductVariant
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Range(35, 47)]
    public int Size { get; set; }

    [Required, MaxLength(60)]
    public string ColorName { get; set; } = string.Empty;

    [Required, MaxLength(12)]
    public string ColorHex { get; set; } = "#4A2E1B";

    [Range(0, 100000)]
    public int PhysicalStock { get; set; }

    [Range(0, 100000)]
    public int ReservedStock { get; set; }

    [NotMapped]
    public int AvailableStock => PhysicalStock - ReservedStock;

    [ConcurrencyCheck]
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
