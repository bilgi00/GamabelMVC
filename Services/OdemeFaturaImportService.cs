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
    // ✅ YENİ: Excel'deki mevcut faturaları filtrele ve kaydet
    // ================================================================
    public async Task<(OtImportBatch? batch, int eklenen, int atlanan, List<string> atlananFaturalar)> ImportAsyncWithFilter(Stream excelStream, string dosyaAdi)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(excelStream);
        if (package.Workbook.Worksheets.Count == 0)
            throw new InvalidOperationException("Excel dosyasinda okunacak bir calisma sayfasi bulunamadi.");

        var sheet = package.Workbook.Worksheets[0];
        var lastRow = sheet.Dimension?.End.Row ?? 1;
        var faturalar = new List<(string cariKart, string faturaNo, decimal bakiye)>();

        // ============================================================
        // 1. Excel'den verileri oku
        // ============================================================
        for (var row = 2; row <= lastRow; row++)
        {
            var cariKart = sheet.Cells[row, 3].Text.Trim();
            var faturaNo = sheet.Cells[row, 6].Text.Trim();
            var bakiyeText = sheet.Cells[row, 12].Text.Trim();

            if (string.IsNullOrWhiteSpace(cariKart) && string.IsNullOrWhiteSpace(faturaNo))
                continue;
            if (string.IsNullOrWhiteSpace(cariKart) || string.IsNullOrWhiteSpace(faturaNo))
                throw new InvalidOperationException($"{row}. satirda cari kart ve fatura no zorunludur.");
            if (!TryParseBakiye(bakiyeText, out var bakiye) || bakiye <= 0)
                throw new InvalidOperationException($"{row}. satirdaki bakiye gecersizdir.");

            faturalar.Add((cariKart, faturaNo, bakiye));
        }

        if (faturalar.Count == 0)
            throw new InvalidOperationException("Excel dosyasinda ice aktarilacak fatura bulunamadi.");

        // ============================================================
        // 2. Excel içinde aynı (CariKart + FaturaNo) kontrolü
        // ============================================================
        var excelDuplicateFaturalar = faturalar
            .GroupBy(f => new { f.cariKart, f.faturaNo })
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.cariKart} - {g.Key.faturaNo}")
            .ToList();

        if (excelDuplicateFaturalar.Any())
        {
            throw new InvalidOperationException(
                $"Excel dosyasinda ayni cari kart ve fatura numarasi birden fazla kez bulunuyor: {string.Join("; ", excelDuplicateFaturalar)}");
        }

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        try
        {
            // ============================================================
            // 3. ✅ Veritabanında mevcut olanları bul
            // ============================================================
            var inClause = string.Join(",", faturalar.Select((_, i) => $"(@cari{i}, @fatura{i})"));
            var kontrolCmd = new MySqlCommand(
                $@"SELECT cari_kart, fatura_no FROM prs_ot_acik_faturalar 
                  WHERE (cari_kart, fatura_no) IN ({inClause})",
                connection, tx);

            for (int i = 0; i < faturalar.Count; i++)
            {
                kontrolCmd.Parameters.AddWithValue($"@cari{i}", faturalar[i].cariKart);
                kontrolCmd.Parameters.AddWithValue($"@fatura{i}", faturalar[i].faturaNo);
            }

            var mevcutSet = new HashSet<(string cariKart, string faturaNo)>();
            await using (var r = await kontrolCmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    mevcutSet.Add((r.GetString(0), r.GetString(1)));
                }
            }

            // ============================================================
            // 4. ✅ Filtrele: Sadece mevcut OLMAYANları kaydet
            // ============================================================
            var eklenecekFaturalar = new List<(string cariKart, string faturaNo, decimal bakiye)>();
            var atlananFaturalar = new List<string>();

            foreach (var f in faturalar)
            {
                if (mevcutSet.Contains((f.cariKart, f.faturaNo)))
                {
                    atlananFaturalar.Add($"{f.cariKart} - {f.faturaNo}");
                }
                else
                {
                    eklenecekFaturalar.Add(f);
                }
            }

            // ============================================================
            // 5. ✅ Eğer eklenecek fatura yoksa uyarı ver
            // ============================================================
            if (eklenecekFaturalar.Count == 0)
            {
                await tx.RollbackAsync();
                throw new InvalidOperationException(
                    $"Excel'deki tüm faturalar zaten sistemde mevcut.\n" +
                    $"Toplam: {faturalar.Count} fatura, tamamı atlandı.");
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
            // 7. Sadece eklenecek faturaları kaydet
            // ============================================================
            foreach (var (cariKart, faturaNo, bakiye) in eklenecekFaturalar)
            {
                var insertFatura = new MySqlCommand(
                    "INSERT INTO prs_ot_acik_faturalar (cari_kart, fatura_no, bakiye, odemeye_dahil_edildi, odeme_durumu, import_batch_id) VALUES (@cari, @fatura, @bakiye, 0, 'bekliyor', @batch)",
                    connection, tx);
                insertFatura.Parameters.AddWithValue("@cari", cariKart);
                insertFatura.Parameters.AddWithValue("@fatura", faturaNo);
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

            return (batch, eklenecekFaturalar.Count, atlananFaturalar.Count, atlananFaturalar);
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

    private static bool TryParseBakiye(string value, out decimal bakiye)
    {
        var styles = NumberStyles.Number | NumberStyles.AllowCurrencySymbol;
        return decimal.TryParse(value, styles, CultureInfo.GetCultureInfo("tr-TR"), out bakiye)
            || decimal.TryParse(value, styles, CultureInfo.InvariantCulture, out bakiye);
    }
}