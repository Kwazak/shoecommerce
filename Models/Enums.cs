namespace KaryaJayaFootwear.Models;

public enum UserRole
{
    CustomerB2C = 0,
    PartnerB2B = 1,
    Admin = 2
}

public enum FootwearCategory
{
    Sepatu = 0,
    Sandal = 1,
    Boot = 2
}

public enum PaymentMethod
{
    MidtransOnline = 0,
    COD = 1,
    TempoB2B = 2
}

public enum ShippingMethod
{
    RegulerKurir = 0,
    EkspedisiKargoTruk = 1
}

public enum OrderStatus
{
    MenungguPembayaran = 0,
    DibayarSiapDikemas = 1,
    DijahitDikemas = 2,
    Dikirim = 3,
    Selesai = 4,
    Dibatalkan = 5,
    Kedaluwarsa = 6
}

public static class EnumExtensions
{
    public static string ToIndonesianLabel(this OrderStatus status) => status switch
    {
        OrderStatus.MenungguPembayaran => "Menunggu Pembayaran",
        OrderStatus.DibayarSiapDikemas => "Dibayar - Siap Dikemas",
        OrderStatus.DijahitDikemas => "Dijahit/Dikemas",
        OrderStatus.Dikirim => "Dikirim",
        OrderStatus.Selesai => "Selesai",
        OrderStatus.Dibatalkan => "Dibatalkan",
        OrderStatus.Kedaluwarsa => "Kedaluwarsa",
        _ => status.ToString()
    };

    public static string ToIndonesianLabel(this PaymentMethod method) => method switch
    {
        PaymentMethod.MidtransOnline => "Pembayaran Online (QRIS / VA / Gerai)",
        PaymentMethod.COD => "COD (Bayar di Tempat)",
        PaymentMethod.TempoB2B => "Tempo Mitra B2B (14 Hari)",
        _ => method.ToString()
    };

    public static string ToIndonesianLabel(this ShippingMethod method) => method switch
    {
        ShippingMethod.RegulerKurir => "Kurir Reguler (Paket Eceran)",
        ShippingMethod.EkspedisiKargoTruk => "Ekspedisi Kargo Truk (Partai Kodian B2B)",
        _ => method.ToString()
    };

    public static string ToIndonesianLabel(this FootwearCategory category) => category switch
    {
        FootwearCategory.Sepatu => "Sepatu",
        FootwearCategory.Sandal => "Sandal",
        FootwearCategory.Boot => "Boot",
        _ => category.ToString()
    };
}
