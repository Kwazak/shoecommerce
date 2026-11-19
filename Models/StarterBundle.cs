using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaryaJayaFootwear.Models;

public class StarterBundle
{
    public int Id { get; set; }

    [Required, MaxLength(140)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public int TotalPairs { get; set; }

    [Column(TypeName = "decimal(18,0)")]
    public decimal BundlePrice { get; set; }

    [Column(TypeName = "decimal(18,0)")]
    public decimal RetailComparePrice { get; set; }

    [Required, MaxLength(60)]
    public string BadgeText { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string IncludedItemsSummary { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string SvgIllustrationKey { get; set; } = "sandal-jepit-karet";

    /// <summary>
    /// JSON map of VariantId -> Quantity so adding a starter bundle adds real warehouse variants.
    /// </summary>
    [Required]
    public string VariantAllocationsJson { get; set; } = "{}";

    public bool IsActive { get; set; } = true;
}
