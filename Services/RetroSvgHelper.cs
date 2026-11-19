using System.Net;
using System.Text;

namespace KaryaJayaFootwear.Services;

public static class RetroSvgHelper
{
    public static IReadOnlyList<(string Key, string Label)> AvailableIllustrationKeys => new[]
    {
        ("sepatu-kanvas", "Sepatu Kanvas Klasik"),
        ("sepatu-olahraga-retro", "Sepatu Kets Olahraga 80an"),
        ("sepatu-kerja-kulit", "Sepatu Pantofel Kerja"),
        ("sepatu-sekolah", "Sepatu Sekolah Hitam"),
        ("sepatu-kets-karet", "Sepatu Slip-On Bengkel"),
        ("sandal-jepit-karet", "Sandal Jepit Karet"),
        ("sandal-kulit-tali", "Sandal Kulit Tali Dua"),
        ("sandal-selop-pabrik", "Sandal Selop Lebar"),
        ("sandal-gunung", "Sandal Gunung Palang"),
        ("boot-kulit-lapangan", "Boot Kulit Lapangan Tinggi"),
        ("boot-karet-proyek", "Boot Karet Proyek/Sawah"),
        ("boot-safety-kerja", "Boot Pendek Pekerja"),
        ("boot-tentara-pendek", "Boot Dinas Rimba")
    };

    /// <summary>
    /// Generates a detailed 1970s-1980s Indonesian print advertisement inline SVG illustration
    /// for shoes, sandals, and boots so zero external image URLs are required.
    /// </summary>
    public static string RenderFootwearSvg(string key, string? primaryHex = null, string? cssClass = "w-full h-full")
    {
        var fill = string.IsNullOrWhiteSpace(primaryHex) ? "#4A2E1B" : WebUtility.HtmlEncode(primaryHex);
        var ink = "#231F20";
        var paper = "#F4EFE6";
        var cream = "#EAE0D0";
        var accent = "#C85A32";
        var green = "#2E4A3E";

        var body = key switch
        {
            "sepatu-kanvas" or "sepatu-sekolah" => $@"
                <g stroke=""{ink}"" stroke-width=""3.2"" stroke-linejoin=""round"" stroke-linecap=""round"">
                  <!-- Halftone print backdrop -->
                  <rect x=""24"" y=""30"" width=""232"" height=""120"" fill=""{cream}"" stroke=""{ink}"" stroke-width=""2"" stroke-dasharray=""5 4""/>
                  <!-- Outsole rubber waffle -->
                  <path d=""M38 128 L236 128 Q244 128 242 138 L236 144 L40 144 Q32 144 34 134 Z"" fill=""{paper}""/>
                  <path d=""M42 136 H234"" stroke=""{ink}"" stroke-width=""2""/>
                  <path d=""M54 144 V139 M70 144 V139 M86 144 V139 M102 144 V139 M118 144 V139 M134 144 V139 M150 144 V139 M166 144 V139 M182 144 V139 M198 144 V139 M214 144 V139"" stroke=""{ink}"" stroke-width=""2""/>
                  <!-- Toe cap -->
                  <path d=""M190 128 Q228 128 240 118 Q243 105 224 102 Q204 100 186 114 Z"" fill=""{paper}""/>
                  <!-- Canvas upper -->
                  <path d=""M44 128 L46 66 Q66 76 92 70 L118 52 L192 98 Q208 106 218 112 L192 128 Z"" fill=""{fill}""/>
                  <!-- Collar & stitching -->
                  <path d=""M52 76 L54 122 M60 80 L62 122 M122 58 L178 96"" stroke=""{paper}"" stroke-width=""1.8"" stroke-dasharray=""4 3""/>
                  <!-- Eyelets & laces -->
                  <circle cx=""120"" cy=""64"" r=""3"" fill=""{paper}""/><circle cx=""134"" cy=""73"" r=""3"" fill=""{paper}""/>
                  <circle cx=""148"" cy=""82"" r=""3"" fill=""{paper}""/><circle cx=""162"" cy=""91"" r=""3"" fill=""{paper}""/>
                  <path d=""M120 64 L132 60 M134 73 L146 69 M148 82 L160 78 M162 91 L174 87"" stroke=""{accent}"" stroke-width=""2.5""/>
                  <!-- Factory foxing stripe -->
                  <path d=""M38 131 H238"" stroke=""{accent}"" stroke-width=""2.4""/>
                </g>",

            "sepatu-olahraga-retro" or "sepatu-kets-karet" => $@"
                <g stroke=""{ink}"" stroke-width=""3.2"" stroke-linejoin=""round"" stroke-linecap=""round"">
                  <circle cx=""140"" cy=""90"" r=""58"" fill=""{cream}"" stroke=""{accent}"" stroke-width=""2"" stroke-dasharray=""6 4""/>
                  <!-- Wedge midsole -->
                  <path d=""M36 122 L242 126 Q248 136 238 143 L38 143 Q30 135 36 122 Z"" fill=""{paper}""/>
                  <path d=""M40 134 H238"" stroke=""{green}"" stroke-width=""3""/>
                  <!-- Upper body -->
                  <path d=""M42 122 L52 64 Q76 74 102 68 L124 48 L194 94 Q228 98 240 116 L240 125 Z"" fill=""{fill}""/>
                  <!-- Retro 70s racing stripes -->
                  <path d=""M96 120 L122 68 L134 73 L110 120 Z"" fill=""{accent}""/>
                  <path d=""M114 120 L138 75 L149 80 L127 120 Z"" fill=""{paper}""/>
                  <!-- Heel stabilizer -->
                  <path d=""M42 122 L48 76 Q64 82 70 122 Z"" fill=""{ink}"" opacity=""0.85""/>
                  <!-- Laces -->
                  <path d=""M130 56 L144 54 M144 66 L158 64 M158 76 L172 74 M172 86 L186 84"" stroke=""{paper}"" stroke-width=""2.5""/>
                </g>",

            "sepatu-kerja-kulit" => $@"
                <g stroke=""{ink}"" stroke-width=""3.2"" stroke-linejoin=""round"" stroke-linecap=""round"">
                  <rect x=""28"" y=""32"" width=""224"" height=""118"" fill=""{cream}"" stroke=""{ink}"" stroke-width=""2""/>
                  <!-- Stacked leather heel & sole -->
                  <path d=""M42 126 H94 V144 H42 Z"" fill=""{ink}""/>
                  <path d=""M94 128 H236 Q244 134 234 139 H94 Z"" fill=""#3A2416""/>
                  <!-- Polished leather upper -->
                  <path d=""M44 126 L50 68 Q78 80 108 72 L134 56 L196 92 Q232 98 242 118 Q242 128 228 128 Z"" fill=""{fill}""/>
                  <!-- Derby wingtip stitch lines -->
                  <path d=""M154 78 Q174 104 166 126"" fill=""none"" stroke=""{paper}"" stroke-width=""1.8"" stroke-dasharray=""3 3""/>
                  <path d=""M192 94 Q206 112 204 126"" fill=""none"" stroke=""{paper}"" stroke-width=""1.8"" stroke-dasharray=""3 3""/>
                  <!-- Gloss highlight -->
                  <path d=""M184 104 Q212 106 226 114"" fill=""none"" stroke=""{paper}"" stroke-width=""2.5"" opacity=""0.65""/>
                </g>",

            "sandal-jepit-karet" => $@"
                <g stroke=""{ink}"" stroke-width=""3.2"" stroke-linejoin=""round"" stroke-linecap=""round"">
                  <rect x=""30"" y=""34"" width=""220"" height=""116"" fill=""{cream}"" stroke=""{ink}"" stroke-width=""2"" stroke-dasharray=""4 4""/>
                  <!-- Thick rubber sponge sole -->
                  <path d=""M40 124 L236 120 Q246 124 244 136 Q240 144 228 144 L46 144 Q34 144 36 130 Z"" fill=""{paper}""/>
                  <path d=""M38 133 H243"" stroke=""{accent}"" stroke-width=""4""/>
                  <path d=""M42 124 H238"" stroke=""{fill}"" stroke-width=""4""/>
                  <!-- Y-strap rubber thong -->
                  <path d=""M88 124 Q118 68 174 80 Q200 86 206 120"" fill=""none"" stroke=""{fill}"" stroke-width=""14""/>
                  <path d=""M88 124 Q118 68 174 80 Q200 86 206 120"" fill=""none"" stroke=""{ink}"" stroke-width=""3""/>
                  <!-- Toe post -->
                  <path d=""M174 80 Q186 102 192 122"" fill=""none"" stroke=""{accent}"" stroke-width=""7""/>
                  <circle cx=""173"" cy=""80"" r=""6"" fill=""{paper}""/>
                </g>",

            "sandal-kulit-tali" or "sandal-selop-pabrik" or "sandal-gunung" => $@"
                <g stroke=""{ink}"" stroke-width=""3.2"" stroke-linejoin=""round"" stroke-linecap=""round"">
                  <rect x=""28"" y=""34"" width=""224"" height=""116"" fill=""{cream}"" stroke=""{ink}"" stroke-width=""2""/>
                  <!-- Ergonomic rugged footbed -->
                  <path d=""M38 118 Q86 124 140 122 Q196 120 238 114 Q246 126 238 140 L44 142 Q32 134 38 118 Z"" fill=""{paper}""/>
                  <path d=""M42 132 H238"" stroke=""{ink}"" stroke-width=""2.5""/>
                  <!-- Lugged grip bottom -->
                  <path d=""M52 142 V147 M72 142 V147 M92 142 V147 M112 142 V147 M132 142 V147 M152 142 V147 M172 142 V147 M192 142 V147 M212 142 V147"" stroke=""{ink}"" stroke-width=""4""/>
                  <!-- Dual leather/webbing straps -->
                  <path d=""M82 120 Q88 74 126 74 L144 120 Z"" fill=""{fill}""/>
                  <path d=""M146 120 Q154 78 190 80 L204 117 Z"" fill=""{fill}""/>
                  <!-- Brass buckles -->
                  <rect x=""98"" y=""82"" width=""18"" height=""12"" fill=""{accent}""/>
                  <rect x=""160"" y=""86"" width=""18"" height=""12"" fill=""{accent}""/>
                  <!-- Stitch marks -->
                  <path d=""M92 114 H136 M156 114 H196"" stroke=""{paper}"" stroke-width=""1.8"" stroke-dasharray=""3 3""/>
                </g>",

            _ => $@"
                <!-- Boots: boot-kulit-lapangan / boot-karet-proyek / boot-safety-kerja / boot-tentara-pendek -->
                <g stroke=""{ink}"" stroke-width=""3.2"" stroke-linejoin=""round"" stroke-linecap=""round"">
                  <rect x=""28"" y=""20"" width=""224"" height=""134"" fill=""{cream}"" stroke=""{ink}"" stroke-width=""2""/>
                  <!-- Heavy commando lug sole -->
                  <path d=""M46 130 H102 V146 H46 Z"" fill=""{ink}""/>
                  <path d=""M102 132 H232 Q240 138 232 146 H102 Z"" fill=""{ink}""/>
                  <path d=""M54 146 V151 M72 146 V151 M90 146 V151 M118 146 V151 M138 146 V151 M158 146 V151 M178 146 V151 M198 146 V151 M218 146 V151"" stroke=""{ink}"" stroke-width=""5""/>
                  <!-- Midsole welt -->
                  <path d=""M46 128 H234"" stroke=""{accent}"" stroke-width=""4""/>
                  <!-- High boot shaft & vamp -->
                  <path d=""M52 128 L56 32 Q84 38 112 32 L126 78 L184 96 Q226 102 234 126 Z"" fill=""{fill}""/>
                  <!-- Padded collar -->
                  <path d=""M56 32 Q84 38 112 32 L114 46 Q84 52 55 46 Z"" fill=""{accent}""/>
                  <!-- Speed hooks & laces -->
                  <circle cx=""118"" cy=""50"" r=""3.2"" fill=""{paper}""/>
                  <circle cx=""123"" cy=""62"" r=""3.2"" fill=""{paper}""/>
                  <circle cx=""128"" cy=""74"" r=""3.2"" fill=""{paper}""/>
                  <circle cx=""144"" cy=""86"" r=""3.2"" fill=""{paper}""/>
                  <circle cx=""160"" cy=""93"" r=""3.2"" fill=""{paper}""/>
                  <path d=""M118 50 L132 48 M123 62 L137 60 M128 74 L142 72 M144 86 L156 80 M160 93 L172 87"" stroke=""{paper}"" stroke-width=""2.4""/>
                  <!-- Reinforced heel & toe stitch -->
                  <path d=""M52 96 Q82 96 86 126"" fill=""none"" stroke=""{paper}"" stroke-width=""2"" stroke-dasharray=""4 3""/>
                  <path d=""M192 98 Q208 110 206 126"" fill=""none"" stroke=""{paper}"" stroke-width=""2"" stroke-dasharray=""4 3""/>
                </g>"
        };

        var sb = new StringBuilder();
        sb.Append($@"<svg viewBox=""0 0 280 170"" xmlns=""http://www.w3.org/2000/svg"" class=""{WebUtility.HtmlEncode(cssClass)}"" role=""img"" aria-label=""Ilustrasi alas kaki pabrik"">");
        sb.Append(body);
        // Retro corner registration marks
        sb.Append($@"<text x=""34"" y=""44"" font-family=""'Courier Prime', monospace"" font-size=""9"" font-weight=""700"" fill=""{ink}"" opacity=""0.65"">KJ-1978</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }

    public static string RenderRubberStampSvg(string topText, string centerText, string bottomText, string colorHex = "#C85A32", int rotationDeg = -8)
    {
        var color = WebUtility.HtmlEncode(colorHex);
        var t1 = WebUtility.HtmlEncode(topText);
        var t2 = WebUtility.HtmlEncode(centerText);
        var t3 = WebUtility.HtmlEncode(bottomText);

        return $@"<svg viewBox=""0 0 220 96"" xmlns=""http://www.w3.org/2000/svg"" class=""w-44 h-20 select-none pointer-events-none"" style=""transform: rotate({rotationDeg}deg);"">
            <rect x=""4"" y=""4"" width=""212"" height=""88"" fill=""none"" stroke=""{color}"" stroke-width=""4"" stroke-dasharray=""10 2 4 2"" opacity=""0.9""/>
            <rect x=""10"" y=""10"" width=""200"" height=""76"" fill=""none"" stroke=""{color}"" stroke-width=""1.8"" opacity=""0.85""/>
            <line x1=""16"" y1=""31"" x2=""204"" y2=""31"" stroke=""{color}"" stroke-width=""1.5""/>
            <line x1=""16"" y1=""66"" x2=""204"" y2=""66"" stroke=""{color}"" stroke-width=""1.5""/>
            <text x=""110"" y=""24"" text-anchor=""middle"" font-family=""'Courier Prime', monospace"" font-size=""10"" font-weight=""700"" letter-spacing=""1.5"" fill=""{color}"">{t1}</text>
            <text x=""110"" y=""55"" text-anchor=""middle"" font-family=""'Courier Prime', monospace"" font-size=""20"" font-weight=""700"" letter-spacing=""2"" fill=""{color}"">{t2}</text>
            <text x=""110"" y=""80"" text-anchor=""middle"" font-family=""'Courier Prime', monospace"" font-size=""9.5"" font-weight=""700"" letter-spacing=""1"" fill=""{color}"">{t3}</text>
        </svg>";
    }
}
