using System.Globalization;

namespace KaryaJayaFootwear.Services;

public static class FormatHelper
{
    private static readonly CultureInfo IdCulture = CultureInfo.GetCultureInfo("id-ID");

    public static string Rupiah(decimal amount)
    {
        return "Rp " + amount.ToString("N0", IdCulture);
    }

    public static string WibDate(DateTime utcDateTime)
    {
        return utcDateTime.AddHours(7).ToString("dd MMM yyyy, HH:mm", IdCulture) + " WIB";
    }

    public static string WibDateShort(DateTime utcDateTime)
    {
        return utcDateTime.AddHours(7).ToString("dd/MM/yyyy", IdCulture);
    }
}
