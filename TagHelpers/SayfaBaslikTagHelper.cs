using Microsoft.AspNetCore.Razor.TagHelpers;

namespace gamabelmvc.TagHelpers;

/// <summary>
/// Standart sayfa üst başlığı (v3.1 tasarım taslağı):
/// solda başlık + açıklama, sağda opsiyonel aksiyon butonları.
///
/// Kullanım:
/// <sayfa-baslik baslik="Puantaj Takvimi"
///               aciklama="Personel çalışma ve izin durumlarını aylık olarak görüntüleyin."
///               ikon="fa-calendar-alt">
///     &lt;button class="btn btn-sm btn-primary"&gt;Önizleme (Yatay)&lt;/button&gt;
///     &lt;button class="btn btn-sm btn-danger"&gt;PDF&lt;/button&gt;
/// </sayfa-baslik>
///
/// - Aksiyon alanı otomatik olarak "no-print" olur (yazdırılan çıktıda görünmez).
/// - Butonlar Bootstrap btn btn-sm ... sınıflarıyla verilir (bkz. CSS_DESIGN_SYSTEM.md).
/// - İçerik verilmezse sağ blok hiç render edilmez.
/// </summary>
[HtmlTargetElement("sayfa-baslik")]
public class SayfaBaslikTagHelper : TagHelper
{
    /// <summary>Sayfa başlığı (zorunlu).</summary>
    [HtmlAttributeName("baslik")]
    public string Baslik { get; set; } = string.Empty;

    /// <summary>Başlık altındaki açıklama metni (opsiyonel).</summary>
    [HtmlAttributeName("aciklama")]
    public string? Aciklama { get; set; }

    /// <summary>Font Awesome ikon adı (opsiyonel; örn. "fa-calendar-alt").</summary>
    [HtmlAttributeName("ikon")]
    public string? Ikon { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrWhiteSpace(Baslik))
            throw new ArgumentException("'baslik' özniteliği zorunludur.");

        output.TagName = "header";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "page-header-card");

        var icerik = await output.GetChildContentAsync();
        var butonlar = icerik.IsEmptyOrWhiteSpace ? null : icerik.GetContent();

        // Sol blok: ikon + başlık + açıklama
        var ikonHtml = string.IsNullOrWhiteSpace(Ikon)
            ? string.Empty
            : $"<i class=\"fas {Ikon!.Trim()} me-2\" aria-hidden=\"true\"></i>";

        var aciklamaHtml = string.IsNullOrWhiteSpace(Aciklama)
            ? string.Empty
            : $"<div class=\"page-header-sub\">{System.Net.WebUtility.HtmlEncode(Aciklama)}</div>";

        output.Content.SetHtmlContent(
            $"<div class=\"page-header-text\">" +
            $"<h1 class=\"page-header-title\">{ikonHtml}{System.Net.WebUtility.HtmlEncode(Baslik)}</h1>" +
            aciklamaHtml +
            "</div>");

        // Sağ blok: aksiyonlar (yazdırılmaz)
        if (butonlar is not null)
        {
            output.Content.AppendHtml("<div class=\"page-header-actions no-print\">");
            output.Content.AppendHtml(butonlar);
            output.Content.AppendHtml("</div>");
        }
    }
}
