using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaryaJayaFootwear.Models;

public class WholesaleTierSetting
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string TierName { get; set; } = string.Empty;

    [Range(1, 10000)]
    public int MinPairs { get; set; }

    public int? MaxPairs { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    [Range(0, 90)]
    public decimal DiscountPercent { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
