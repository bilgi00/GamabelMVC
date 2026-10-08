using System.Globalization;
using MySqlConnector;
using OfficeOpenXml;
using gamabelmvc.Models.PRS;

namespace gamabelmvc.Services;

public class OdemeFaturaImportService
{
    private readonly string _connectionString;

    public OdemeFaturaImportService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MyConnection")
            ?? throw new InvalidOperationException("MyConnection tanimli degil.");
    }

    // ================================================================
    // ✅ YENİ: Excel'deki mevcut faturaları filtrele ve kaydet (Tarih Eklendi)
    // ================================================================
    public async Task<(
        OtImportBatch? batch,
        int excelSatirSayisi,
        int ciftIslemSayisi,
        int odenmisIslemSayisi,
        int mevcutAcikIslemSayisi,
        int eklenen,
        List<string> odenmisFaturalar,
        List<string> ciftFaturalar)> ImportAsyncWithFilter(Stream excelStream, string dosyaAdi)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(excelStream);
        if (package.Workbook.Worksheets.Count == 0)
            throw new InvalidOperationException("Excel dosyasinda okunacak bir calisma sayfasi bulunamadi.");

        var sheet = package.Workbook.Worksheets[0];
        var lastRow = sheet.Dimension?.End.Row ?? 1;
        
        // ✅ Fatura verilerini tutacak liste (tarih eklendi)
        var faturalar = new List<(string cariKart, string faturaNo, DateTime? faturaTarihi, decimal bakiye)>();

        // ============================================================
        // 1. Excel'den verileri oku
        // ============================================================
        for (var row = 2; row <= lastRow; row++)
        {
            var cariKart = sheet.Cells[row, 3].Text.Trim();        // C sütunu - Cari Kart
            var faturaNo = sheet.Cells[row, 6].Text.Trim();        // F sütunu - Fatura No
            var faturaTarihiText = sheet.Cells[row, 7].Text.Trim(); // ✅ G sütunu - Fatura Tarihi
            var bakiyeText = sheet.Cells[row, 12].Text.Trim();     // L sütunu - Bakiye

            if (string.IsNullOrWhiteSpace(cariKart) && string.IsNullOrWhiteSpace(faturaNo))
                continue;
            if (string.IsNullOrWhiteSpace(cariKart) || string.IsNullOrWhiteSpace(faturaNo))
                throw new InvalidOperationException($"{row}. satirda cari kart ve fatura no zorunludur.");
            if (!TryParseBakiye(bakiyeText, out var bakiye) || bakiye <= 0)
                throw new InvalidOperationException($"{row}. satirdaki bakiye gecersizdir.");

            // ✅ Fatura tarihini parse et
            DateTime? faturaTarihi = null;
            if (!string.IsNullOrWhiteSpace(faturaTarihiText))
            {
                // Önce Türkçe format dene (dd.MM.yyyy)
                if (DateTime.TryParseExact(faturaTarihiText, "dd.MM.yyyy", CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, out var tarih1))
                {
                    faturaTarihi = tarih1;
                }
                // Sonra genel format dene
                else if (DateTime.TryParse(faturaTarihiText, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, out var tarih2))
                {
                    faturaTarihi = tarih2;
                }
                else if (DateTime.TryParse(faturaTarihiText, out var tarih3))
                {
                    faturaTarihi = tarih3;
                }
                else
                {
                    throw new InvalidOperationException($"{row}. satirdaki fatura tarihi gecersizdir: {faturaTarihiText}");
                }
            }

            faturalar.Add((cariKart, faturaNo, faturaTarihi, bakiye));
        }

        if (faturalar.Count == 0)
            throw new InvalidOperationException("Excel dosyasinda ice aktarilacak fatura bulunamadi.");

        var excelGruplari = faturalar
            .GroupBy(f => (f.cariKart, f.faturaNo), InvoiceKeyComparer.Instance)
            .ToList();
        var ciftIslemSayisi = excelGruplari.Sum(g => g.Count() - 1);
        var ciftFaturalar = excelGruplari
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.First().cariKart} - {g.First().faturaNo}")
            .ToList();
        var benzersizFaturalar = excelGruplari.Select(g => g.First()).ToList();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        try
        {
            // ============================================================
            // 3. ✅ Veritabanında mevcut olanları bul (CariKart + FaturaNo)
            // ============================================================
            var inClause = string.Join(",", benzersizFaturalar.Select((_, i) => $"(@cari{i}, @fatura{i})"));
            var kontrolCmd = new MySqlCommand(
                $@"SELECT cari_kart, fatura_no, odeme_durumu FROM prs_ot_acik_faturalar
                  WHERE (cari_kart, fatura_no) IN ({inClause})",
                connection, tx);

            for (int i = 0; i < benzersizFaturalar.Count; i++)
            {
                kontrolCmd.Parameters.AddWithValue($"@cari{i}", benzersizFaturalar[i].cariKart);
                kontrolCmd.Parameters.AddWithValue($"@fatura{i}", benzersizFaturalar[i].faturaNo);
            }

            var mevcutSet = new HashSet<(string cariKart, string faturaNo)>(InvoiceKeyComparer.Instance);
            var odenmisSet = new HashSet<(string cariKart, string faturaNo)>(InvoiceKeyComparer.Instance);
            await using (var r = await kontrolCmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    var key = (r.GetString(0).Trim(), r.GetString(1).Trim());
                    mevcutSet.Add(key);
                    if (string.Equals(r.GetString(2), "odendi", StringComparison.OrdinalIgnoreCase))
                        odenmisSet.Add(key);
                }
            }

            // ============================================================
            // 4. Tekrarlanan ve daha önce yüklenmiş faturaları filtrele
            // ============================================================
            var eklenecekFaturalar = new List<(string cariKart, string faturaNo, DateTime? faturaTarihi, decimal bakiye)>();
            var odenmisFaturalar = new List<string>();
            var mevcutAcikIslemSayisi = 0;

            foreach (var f in benzersizFaturalar)
            {
                var key = (f.cariKart, f.faturaNo);
                if (odenmisSet.Contains(key))
                {
                    odenmisFaturalar.Add($"{f.cariKart} - {f.faturaNo}");
                }
                else if (mevcutSet.Contains(key))
                {
                    mevcutAcikIslemSayisi++;
                }
                else
                {
                    eklenecekFaturalar.Add(f);
                }
            }

            // ============================================================
            // 5. Hiç yeni fatura yoksa raporu döndür; boş batch oluşturma
            // ============================================================
            if (eklenecekFaturalar.Count == 0)
            {
                await tx.RollbackAsync();
                return (
                    null,
                    faturalar.Count,
                    ciftIslemSayisi,
                    odenmisFaturalar.Count,
                    mevcutAcikIslemSayisi,
                    0,
                    odenmisFaturalar,
                    ciftFaturalar);
            }

            // ============================================================
            // 6. Batch kaydı oluştur
            // ============================================================
            var insertBatch = new MySqlCommand(
                "INSERT INTO prs_ot_import_batchlari (dosya_adi, yukleme_tarihi, satir_sayisi) VALUES (@dosya, NOW(), @sayi)",
                connection, tx);
            insertBatch.Parameters.AddWithValue("@dosya", dosyaAdi);
            insertBatch.Parameters.AddWithValue("@sayi", eklenecekFaturalar.Count);
            await insertBatch.ExecuteNonQueryAsync();
            var batchId = (int)insertBatch.LastInsertedId;

            // ============================================================
            // 7. ✅ Sadece eklenecek faturaları kaydet (tarih ile birlikte)
            // ============================================================
            foreach (var (cariKart, faturaNo, faturaTarihi, bakiye) in eklenecekFaturalar)
            {
                var insertFatura = new MySqlCommand(
                    @"INSERT INTO prs_ot_acik_faturalar 
                      (cari_kart, fatura_no, fatura_tarihi, bakiye, odemeye_dahil_edildi, odeme_durumu, import_batch_id) 
                      VALUES (@cari, @fatura, @tarih, @bakiye, 0, 'bekliyor', @batch)",
                    connection, tx);
                insertFatura.Parameters.AddWithValue("@cari", cariKart);
                insertFatura.Parameters.AddWithValue("@fatura", faturaNo);
                insertFatura.Parameters.AddWithValue("@tarih", faturaTarihi.HasValue ? faturaTarihi.Value : (object)DBNull.Value);
                insertFatura.Parameters.AddWithValue("@bakiye", bakiye);
                insertFatura.Parameters.AddWithValue("@batch", batchId);
                await insertFatura.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();

            var batch = new OtImportBatch 
            { 
                Id = batchId, 
                DosyaAdi = dosyaAdi, 
                YuklemeTarihi = DateTime.Now, 
                SatirSayisi = eklenecekFaturalar.Count 
            };

            return (
                batch,
                faturalar.Count,
                ciftIslemSayisi,
                odenmisFaturalar.Count,
                mevcutAcikIslemSayisi,
                eklenecekFaturalar.Count,
                odenmisFaturalar,
                ciftFaturalar);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ================================================================
    // ESKİ METOT (Geriye Dönük Uyumluluk)
    // ================================================================
    public async Task<OtImportBatch> ImportAsync(Stream excelStream, string dosyaAdi)
    {
        var result = await ImportAsyncWithFilter(excelStream, dosyaAdi);
        if (result.batch == null)
            throw new InvalidOperationException("Kaydedilecek fatura bulunamadı.");
        return result.batch;
    }

    private sealed class InvoiceKeyComparer : IEqualityComparer<(string cariKart, string faturaNo)>
    {
        public static readonly InvoiceKeyComparer Instance = new();

        public bool Equals((string cariKart, string faturaNo) x, (string cariKart, string faturaNo) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.cariKart, y.cariKart)
            && StringComparer.OrdinalIgnoreCase.Equals(x.faturaNo, y.faturaNo);

        public int GetHashCode((string cariKart, string faturaNo) value) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.cariKart),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.faturaNo));
    }

    private static bool TryParseBakiye(string value, out decimal bakiye)
    {
        var styles = NumberStyles.Number | NumberStyles.AllowCurrencySymbol;
        return decimal.TryParse(value, styles, CultureInfo.GetCultureInfo("tr-TR"), out bakiye)
            || decimal.TryParse(value, styles, CultureInfo.InvariantCulture, out bakiye);
    }
}