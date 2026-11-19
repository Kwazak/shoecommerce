using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaryaJayaFootwear.Models;

public class Product
{
    public int Id { get; set; }

    [Required, MaxLength(32)]
    public string Sku { get; set; } = string.Empty;

    [Required, MaxLength(140)]
    public string Name { get; set; } = string.Empty;

    public FootwearCategory Category { get; set; }

    [Required, MaxLength(180)]
    public string ShortTagline { get; set; } = string.Empty;

    [Required, MaxLength(1500)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string UpperMaterial { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string SoleMaterial { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string ConstructionMethod { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,0)")]
    public decimal RetailPrice { get; set; }

    [Required, MaxLength(60)]
    public string SvgIllustrationKey { get; set; } = "sepatu-kanvas";

    [MaxLength(300)]
    public string? ImageUrl { get; set; }

    public bool IsFeatured { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}
