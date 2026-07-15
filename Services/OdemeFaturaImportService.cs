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

    public async Task<OtImportBatch> ImportAsync(Stream excelStream, string dosyaAdi)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(excelStream);
        if (package.Workbook.Worksheets.Count == 0)
            throw new InvalidOperationException("Excel dosyasinda okunacak bir calisma sayfasi bulunamadi.");

        var sheet = package.Workbook.Worksheets[0];
        var lastRow = sheet.Dimension?.End.Row ?? 1;
        var faturalar = new List<(string cariKart, string faturaNo, decimal bakiye)>();

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

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            var insertBatch = new MySqlCommand(
                "INSERT INTO prs_ot_import_batchlari (dosya_adi, yukleme_tarihi, satir_sayisi) VALUES (@dosya, NOW(), @sayi)",
                connection, tx);
            insertBatch.Parameters.AddWithValue("@dosya", dosyaAdi);
            insertBatch.Parameters.AddWithValue("@sayi", faturalar.Count);
            await insertBatch.ExecuteNonQueryAsync();
            var batchId = (int)insertBatch.LastInsertedId;

            foreach (var (cariKart, faturaNo, bakiye) in faturalar)
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
            return new OtImportBatch { Id = batchId, DosyaAdi = dosyaAdi, YuklemeTarihi = DateTime.Now, SatirSayisi = faturalar.Count };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static bool TryParseBakiye(string value, out decimal bakiye)
    {
        var styles = NumberStyles.Number | NumberStyles.AllowCurrencySymbol;
        return decimal.TryParse(value, styles, CultureInfo.GetCultureInfo("tr-TR"), out bakiye)
            || decimal.TryParse(value, styles, CultureInfo.InvariantCulture, out bakiye);
    }
}
