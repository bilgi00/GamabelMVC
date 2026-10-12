using System.Data;
using FastReport;
using FastReport.Data;
using FastReport.Export.Html;
using FastReport.Export.PdfSimple;
using FastReport.Web;

namespace gamabelmvc.Services.Raporlama;

/// <summary>
/// FastReport A4 Rapor Standardı (v3) ortak rapor servisi.
/// Önizleme ve PDF aynı .frx şablonundan üretilir; tasarım şablondan okunur.
/// </summary>
public sealed class RaporServisi : IRaporServisi
{
    private readonly IWebHostEnvironment _env;

    public RaporServisi(IWebHostEnvironment env) => _env = env;

    public WebReport Onizleme(RaporTanimi t, RaporYonu yon, DataSet veri, IDictionary<string, object?> prm)
    {
        var web = new WebReport();
        Hazirla(web.Report, t, yon, veri, prm);

        web.Width = "100%";
        web.Height = "calc(100vh - 24px)"; // üstte 24px yön seçici çubuğu için yer bırakır
        web.Inline = false;

        web.Toolbar.Show = true;
        web.Toolbar.ShowPrint = true;
        web.Toolbar.Exports.Show = true;
        web.Toolbar.Exports.ShowPreparedReport = true;
        return web;
    }

    public RaporCikti Pdf(RaporTanimi t, RaporYonu yon, DataSet veri, IDictionary<string, object?> prm)
        => Cikar(t, RaporCiktiTuru.Pdf, yon, veri, prm);

    /// <summary>PDF veya HTML çıktısı — ikisi de aynı .frx şablonundan üretilir.</summary>
    public RaporCikti Cikar(RaporTanimi t, RaporCiktiTuru tur, RaporYonu yon, DataSet veri, IDictionary<string, object?> prm)
    {
        using var report = new Report();
        Hazirla(report, t, yon, veri, prm);
        report.Prepare();

        using var ms = new MemoryStream();
        switch (tur)
        {
            case RaporCiktiTuru.Html:
                // Görselleri HTML içine gömer → tek dosyalık taşınabilir çıktı.
                using (var export = new HTMLExport())
                {
                    export.EmbedPictures = true;
                    export.Export(report, ms);
                }
                break;
            case RaporCiktiTuru.Pdf:
            default:
                using (var export = new PDFSimpleExport())
                {
                    export.Export(report, ms);
                }
                break;
        }
        return new RaporCikti(ms.ToArray(), report.ReportInfo.Version ?? "0");
    }

    private void Hazirla(Report report, RaporTanimi t, RaporYonu yon, DataSet veri, IDictionary<string, object?> prm)
    {
        var frx = Path.Combine(_env.ContentRootPath, "Reports", t.Modul, $"{t.Ad}.{yon}.frx");
        if (!File.Exists(frx))
            throw new FileNotFoundException($"Rapor dosyası bulunamadı: {frx}");

        // H4: Tablo adı sabit yazılmaz; beklenen tabloların varlığı denetlenir, hepsi etkinleştirilir.
        foreach (var tablo in t.Tablolar)
            if (!veri.Tables.Contains(tablo))
                throw new InvalidOperationException(
                    $"'{t.Ad}' raporu için '{tablo}' tablosu DataSet içinde yok. Mevcut: " +
                    string.Join(", ", veri.Tables.Cast<DataTable>().Select(x => x.TableName)));

        BosVeriYerTutucuEkle(veri, t.Tablolar);

        report.Load(frx);
        report.RegisterData(veri, "Data");
        foreach (DataSourceBase kaynak in report.Dictionary.DataSources)
            kaynak.Enabled = true;

        foreach (var kv in prm)
            report.SetParameterValue(kv.Key, kv.Value);

        report.SetParameterValue("SablonSurum", report.ReportInfo.Version ?? "0");

        // Ortak kurumsal öğeler (Bölüm 10): yalnızca şablonda karşılığı varsa uygulanır.
        if (report.FindObject("PicLogo") is PictureObject logo)
            logo.ImageLocation = Path.Combine(_env.WebRootPath, "img", "logo.png");

        if (prm.TryGetValue("Filigran", out var f) && f is string filigran
            && !string.IsNullOrWhiteSpace(filigran)
            && report.Pages.Count > 0 && report.Pages[0] is ReportPage sayfa)
        {
            sayfa.Watermark.Enabled = true;
            sayfa.Watermark.Text = filigran; // örn. "TASLAK"
        }
    }

    /// <summary>
    /// Boş DataBand bazı FRX düzenlerinde sıfır hazırlanmış sayfa bırakır ve önizlemede
    /// PreparedPages.GetPageSize(0) kaynaklı ArgumentOutOfRangeException verir.
    /// Boş tabloya, aynı kolonları taşıyan tek bir "Kayıt bulunamadı" satırı ekler.
    /// Gerçek kayıt sayısı controller'dan KayitSayisi parametresiyle ayrı iletilir.
    /// </summary>
    private static void BosVeriYerTutucuEkle(DataSet veri, string[] tablolar)
    {
        foreach (var ad in tablolar)
        {
            var tablo = veri.Tables[ad];
            if (tablo is null || tablo.Rows.Count > 0) continue;

            var ilkMetin = tablo.Columns.Cast<DataColumn>()
                .FirstOrDefault(c => c.DataType == typeof(string));

            var satir = tablo.NewRow();
            foreach (DataColumn kolon in tablo.Columns)
            {
                if (kolon.DataType == typeof(string))
                    satir[kolon] = kolon.Equals(ilkMetin) ? "Kayıt bulunamadı" : string.Empty;
                else if (kolon.DataType == typeof(decimal))
                    satir[kolon] = 0m;
                else if (kolon.DataType == typeof(int))
                    satir[kolon] = 0;
            }
            tablo.Rows.Add(satir);
        }
    }
}
