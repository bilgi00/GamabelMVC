using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using gamabelmvc.Data;
using gamabelmvc.Models.StokSayim;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using MySqlConnector;

namespace gamabelmvc.Services.StokSayim;

public sealed class SistemStokExcelService(
    StokSayimDbContext dbContext,
    IMemoryCache cache) : ISistemStokExcelService
{
    public const long MaksimumDosyaBoyutu = 20 * 1024 * 1024;
    public const int MaksimumExcelSatirSayisi = 50_000;
    private const int OrnekSatirSayisi = 100;
    private static readonly TimeSpan OnizlemeOmru = TimeSpan.FromMinutes(20);

    public byte[] SablonOlustur()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sistem Stoğu");
        var headers = new[]
        {
            "Stok Kodu", "Referans Kodu", "Aktif Barkod", "Stok Adı", "Giren Miktar",
            "Çıkan Miktar", "Kalan Miktar", "Birim", "Alış Fiyatı", "Satış Fiyatı", "P. Cinsi"
        };
        for (var i = 0; i < headers.Length; i++)
            worksheet.Cell(1, i + 1).Value = headers[i];
        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.Columns(1, 3).Style.NumberFormat.Format = "@";
        worksheet.Column(4).Width = 35;
        worksheet.Columns(5, 7).Style.NumberFormat.Format = "#,##0.000";
        worksheet.Column(8).Width = 14;
        worksheet.Columns(9, 10).Style.NumberFormat.Format = "#,##0.0000";
        worksheet.Column(11).Width = 18;
        worksheet.Range("A1:K1").SetAutoFilter();
        worksheet.SheetView.FreezeRows(1);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<SistemStokOnizlemeViewModel> OnizleAsync(
        int sayimId,
        IFormFile dosya,
        string kullanici,
        CancellationToken cancellationToken = default)
    {
        if (dosya == null || dosya.Length == 0)
            throw new InvalidOperationException("Lütfen sistem stoğu Excel dosyası seçin.");
        if (dosya.Length > MaksimumDosyaBoyutu)
            throw new InvalidOperationException("Excel dosyası 20 MB sınırını aşamaz.");
        if (!string.Equals(Path.GetExtension(dosya.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Yalnızca .xlsx uzantılı Excel dosyaları kabul edilir.");

        var sayim = await dbContext.Sayimlar
            .AsNoTracking()
            .Where(s => s.Id == sayimId)
            .Select(s => new { s.Id, s.Durum })
            .SingleOrDefaultAsync(cancellationToken);
        if (sayim == null)
            throw new InvalidOperationException("Sayım kaydı bulunamadı.");
        if (sayim.Durum != "Açık")
            throw new InvalidOperationException("Kapalı sayıma sistem stoğu yüklenemez.");

        await using var stream = new MemoryStream((int)dosya.Length);
        await dosya.CopyToAsync(stream, cancellationToken);
        if (stream.Length < 4 || stream.GetBuffer()[0] != (byte)'P' || stream.GetBuffer()[1] != (byte)'K')
            throw new InvalidOperationException("Dosya geçerli bir .xlsx Excel dosyası değil.");

        stream.Position = 0;
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (OpenXmlPackageException ex)
        {
            throw new InvalidOperationException("Dosya geçerli bir .xlsx çalışma kitabı değil.", ex);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidOperationException("Dosya geçerli bir .xlsx çalışma kitabı değil.", ex);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("Dosya geçerli bir .xlsx çalışma kitabı değil.", ex);
        }

        using (workbook)
        {
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("Excel dosyasında çalışma sayfası bulunamadı.");
        var lastRow = worksheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 0;
        var lastColumn = worksheet.LastColumnUsed(XLCellsUsedOptions.Contents)?.ColumnNumber() ?? 0;
        if (lastRow < 1 || lastColumn < 1)
            throw new InvalidOperationException("Excel dosyası boş.");

        var stokKoduColumn = FindHeaderColumn(worksheet, lastColumn, "Stok Kodu");
        var referansKoduColumn = FindHeaderColumn(worksheet, lastColumn, "Referans Kodu");
        var barkodColumn = FindHeaderColumn(worksheet, lastColumn, "Aktif Barkod", "Barkod");
        var urunAdiColumn = FindHeaderColumn(worksheet, lastColumn, "Stok Adı", "Ürün Adı");
        var girenMiktarColumn = FindHeaderColumn(worksheet, lastColumn, "Giren Miktar");
        var cikanMiktarColumn = FindHeaderColumn(worksheet, lastColumn, "Çıkan Miktar");
        var stokColumn = FindHeaderColumn(worksheet, lastColumn, "Kalan Miktar", "Sistem Stoğu");
        var birimColumn = FindHeaderColumn(worksheet, lastColumn, "Birim");
        var alisFiyatiColumn = FindHeaderColumn(worksheet, lastColumn, "Alış Fiyatı");
        var satisFiyatiColumn = FindHeaderColumn(worksheet, lastColumn, "Satış Fiyatı");
        var pCinsiColumn = FindHeaderColumn(worksheet, lastColumn, "P. Cinsi");
        if (barkodColumn == 0 || urunAdiColumn == 0 || stokColumn == 0)
            throw new InvalidOperationException(
                "Başlık satırında 'Aktif Barkod', 'Stok Adı' ve 'Kalan Miktar' sütunları bulunmalıdır.");
        var dataColumns = new[]
        {
            stokKoduColumn, referansKoduColumn, barkodColumn, urunAdiColumn, girenMiktarColumn,
            cikanMiktarColumn, stokColumn, birimColumn, alisFiyatiColumn, satisFiyatiColumn, pCinsiColumn
        }.Where(column => column > 0).ToArray();

        var validRows = new List<SistemStokSatiriOnizlemeViewModel>();
        var invalidRows = new List<SistemStokSatiriOnizlemeViewModel>();
        var seenBarcodes = new HashSet<string>(StringComparer.Ordinal);
        var dataRowCount = 0;

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (dataColumns.All(column => worksheet.Cell(rowNumber, column).IsEmpty()))
                continue;

            dataRowCount++;
            if (dataRowCount > MaksimumExcelSatirSayisi)
                throw new InvalidOperationException(
                    $"Excel dosyası en fazla {MaksimumExcelSatirSayisi:N0} dolu satır içerebilir.");

            var barcodeCell = worksheet.Cell(rowNumber, barkodColumn);
            var nameCell = worksheet.Cell(rowNumber, urunAdiColumn);
            var barcode = ReadBarcode(barcodeCell, out var barcodeError);
            var name = nameCell.GetFormattedString().Trim();
            var errorReasons = new List<string>();
            var stokKodu = ReadOptionalText(worksheet, rowNumber, stokKoduColumn, 100, "Stok Kodu", errorReasons);
            var referansKodu = ReadOptionalText(worksheet, rowNumber, referansKoduColumn, 100, "Referans Kodu", errorReasons);
            if (!string.IsNullOrEmpty(barcodeError))
                errorReasons.Add(barcodeError);
            else if (!string.IsNullOrEmpty(barcode) && !seenBarcodes.Add(barcode))
                errorReasons.Add("Barkod dosyada birden fazla kez kullanılmış.");

            if (string.IsNullOrWhiteSpace(name))
                errorReasons.Add("Ürün adı boş olamaz.");
            else if (name.Length > 300)
                errorReasons.Add("Ürün adı en fazla 300 karakter olabilir.");

            var stockCell = worksheet.Cell(rowNumber, stokColumn);
            var stockIsValid = TryReadStock(stockCell, out var stock);
            if (!stockIsValid)
                errorReasons.Add("Kalan miktar negatif veya pozitif olabilir ve en fazla üç ondalık basamaklı olmalıdır.");

            var girenMiktar = ReadOptionalDecimal(
                worksheet, rowNumber, girenMiktarColumn, 3, 999999999999999.999m, "Giren Miktar", errorReasons,
                allowNegative: true);
            var cikanMiktar = ReadOptionalDecimal(
                worksheet, rowNumber, cikanMiktarColumn, 3, 999999999999999.999m, "Çıkan Miktar", errorReasons);
            var birim = ReadOptionalText(worksheet, rowNumber, birimColumn, 50, "Birim", errorReasons);
            var alisFiyati = ReadOptionalDecimal(
                worksheet, rowNumber, alisFiyatiColumn, 4, 99999999999999.9999m, "Alış Fiyatı", errorReasons);
            var satisFiyati = ReadOptionalDecimal(
                worksheet, rowNumber, satisFiyatiColumn, 4, 99999999999999.9999m, "Satış Fiyatı", errorReasons);
            var pCinsi = ReadOptionalText(worksheet, rowNumber, pCinsiColumn, 100, "P. Cinsi", errorReasons);

            var previewRow = new SistemStokSatiriOnizlemeViewModel
            {
                SatirNo = rowNumber,
                StokKodu = stokKodu,
                ReferansKodu = referansKodu,
                Barkod = barcode,
                UrunAdi = name,
                GirenMiktar = girenMiktar,
                CikanMiktar = cikanMiktar,
                SistemStok = stockIsValid ? stock : null,
                Birim = birim,
                AlisFiyati = alisFiyati,
                SatisFiyati = satisFiyati,
                PCinsi = pCinsi,
                Hata = string.Join(" ", errorReasons)
            };
            if (errorReasons.Count == 0)
                validRows.Add(previewRow);
            else
                invalidRows.Add(previewRow);
        }

        if (dataRowCount == 0)
            throw new InvalidOperationException("Excel dosyasında başlık dışında ürün satırı bulunamadı.");

        var token = Guid.NewGuid().ToString("N");
        var fileName = Path.GetFileName(dosya.FileName);
        if (fileName.Length > 255)
            fileName = fileName[..255];
        var preview = new CachedPreview(
            sayimId,
            kullanici,
            fileName,
            validRows,
            invalidRows);
        cache.Set(GetCacheKey(token), preview, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = OnizlemeOmru
        });
        var ownerCacheKey = GetOwnerCacheKey(sayimId, kullanici);
        if (cache.TryGetValue(ownerCacheKey, out string? previousToken) && previousToken != null)
            cache.Remove(GetCacheKey(previousToken));
        cache.Set(ownerCacheKey, token, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = OnizlemeOmru
        });

        return ToViewModel(token, preview);
        }
    }

    public SistemStokOnizlemeViewModel? OnizlemeGetir(int sayimId, string token, string kullanici)
    {
        var preview = GetOwnedPreview(sayimId, token, kullanici);
        return preview == null ? null : ToViewModel(token, preview);
    }

    public byte[] HataliSatirlariExcelOlarakOlustur(int sayimId, string token, string kullanici)
    {
        var preview = GetOwnedPreview(sayimId, token, kullanici)
            ?? throw new InvalidOperationException("Önizleme bulunamadı veya süresi doldu. Excel dosyasını yeniden yükleyin.");

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Hatalı Satırlar");
        worksheet.Cell(1, 1).Value = "Excel Satır No";
        var headers = new[]
        {
            "Stok Kodu", "Referans Kodu", "Aktif Barkod", "Stok Adı", "Giren Miktar",
            "Çıkan Miktar", "Kalan Miktar", "Birim", "Alış Fiyatı", "Satış Fiyatı", "P. Cinsi", "Hata Nedeni"
        };
        for (var i = 0; i < headers.Length; i++)
            worksheet.Cell(1, i + 2).Value = headers[i];
        worksheet.Row(1).Style.Font.Bold = true;

        for (var i = 0; i < preview.InvalidRows.Count; i++)
        {
            var row = preview.InvalidRows[i];
            var targetRow = i + 2;
            worksheet.Cell(targetRow, 1).Value = row.SatirNo;
            worksheet.Cell(targetRow, 2).Value = row.StokKodu;
            worksheet.Cell(targetRow, 3).Value = row.ReferansKodu;
            worksheet.Cell(targetRow, 4).Value = row.Barkod;
            worksheet.Cell(targetRow, 5).Value = row.UrunAdi;
            if (row.GirenMiktar.HasValue)
                worksheet.Cell(targetRow, 6).Value = row.GirenMiktar.Value;
            if (row.CikanMiktar.HasValue)
                worksheet.Cell(targetRow, 7).Value = row.CikanMiktar.Value;
            if (row.SistemStok.HasValue)
                worksheet.Cell(targetRow, 8).Value = row.SistemStok.Value;
            worksheet.Cell(targetRow, 9).Value = row.Birim;
            if (row.AlisFiyati.HasValue)
                worksheet.Cell(targetRow, 10).Value = row.AlisFiyati.Value;
            if (row.SatisFiyati.HasValue)
                worksheet.Cell(targetRow, 11).Value = row.SatisFiyati.Value;
            worksheet.Cell(targetRow, 12).Value = row.PCinsi;
            worksheet.Cell(targetRow, 13).Value = row.Hata;
        }

        worksheet.Columns().AdjustToContents();
        worksheet.SheetView.FreezeRows(1);
        worksheet.RangeUsed()?.SetAutoFilter();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<int> OnizlemeyiKaydetAsync(
        int sayimId,
        string token,
        string kullanici,
        CancellationToken cancellationToken = default)
    {
        var preview = GetOwnedPreview(sayimId, token, kullanici)
            ?? throw new InvalidOperationException("Önizleme bulunamadı veya süresi doldu. Excel dosyasını yeniden yükleyin.");
        if (preview.InvalidRows.Count != 0 || preview.ValidRows.Count == 0)
            throw new InvalidOperationException("Hatalı satırlar düzeltilmeden sistem stoğu kaydedilemez.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var sayim = await dbContext.Sayimlar
            .SingleOrDefaultAsync(s => s.Id == sayimId, cancellationToken);
        if (sayim == null)
            throw new InvalidOperationException("Sayım kaydı bulunamadı.");
        if (sayim.Durum != "Açık")
            throw new InvalidOperationException("Kapalı sayıma sistem stoğu yüklenemez.");

        var hareketVarMi = await dbContext.SayimHareketleri
            .AnyAsync(h => h.SayimId == sayimId, cancellationToken);
        if (hareketVarMi)
            throw new InvalidOperationException(
                "Sayım hareketi başladıktan sonra sistem stoğu değiştirilemez. Mevcut snapshot korunuyor.");

        await dbContext.SayimSistemStoklari
            .Where(s => s.SayimId == sayimId)
            .ExecuteDeleteAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var uploadGroup = new SayimYuklemeGrubu
        {
            SayimId = sayimId,
            Tur = "SistemStok",
            DosyaAdi = preview.FileName,
            Kullanici = kullanici,
            Zaman = now,
            SatirSayisi = preview.ValidRows.Count,
            HataliSatirSayisi = preview.InvalidRows.Count
        };
        dbContext.SayimYuklemeGruplari.Add(uploadGroup);
        dbContext.DenetimKayitlari.Add(new DenetimKaydi
        {
            Zaman = now,
            Kullanici = kullanici,
            Islem = "Sistem stoğu yüklendi",
            HedefId = sayimId,
            Detay = $"{preview.FileName}: {preview.ValidRows.Count:N0} ürün satırı."
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var connection = (MySqlConnection)dbContext.Database.GetDbConnection();
        var dbTransaction = (MySqlTransaction)transaction.GetDbTransaction();
        const int bulkBatchSize = 500;
        for (var offset = 0; offset < preview.ValidRows.Count; offset += bulkBatchSize)
        {
            var count = Math.Min(bulkBatchSize, preview.ValidRows.Count - offset);
            var values = new string[count];
            await using var command = new MySqlCommand
            {
                Connection = connection,
                Transaction = dbTransaction
            };
            for (var i = 0; i < count; i++)
            {
                var row = preview.ValidRows[offset + i];
                var parameterPrefix = $"r{i}";
                values[i] =
                    $"(@sayim{parameterPrefix}, @stokKodu{parameterPrefix}, @referansKodu{parameterPrefix}, " +
                    $"@barkod{parameterPrefix}, @urun{parameterPrefix}, @giren{parameterPrefix}, " +
                    $"@cikan{parameterPrefix}, @stok{parameterPrefix}, @birim{parameterPrefix}, " +
                    $"@alis{parameterPrefix}, @satis{parameterPrefix}, @pCinsi{parameterPrefix})";
                command.Parameters.AddWithValue($"@sayim{parameterPrefix}", sayimId);
                command.Parameters.AddWithValue($"@stokKodu{parameterPrefix}", (object?)row.StokKodu ?? DBNull.Value);
                command.Parameters.AddWithValue($"@referansKodu{parameterPrefix}", (object?)row.ReferansKodu ?? DBNull.Value);
                command.Parameters.AddWithValue(
                    $"@barkod{parameterPrefix}",
                    string.IsNullOrWhiteSpace(row.Barkod) ? DBNull.Value : row.Barkod);
                command.Parameters.AddWithValue($"@urun{parameterPrefix}", row.UrunAdi);
                command.Parameters.AddWithValue($"@giren{parameterPrefix}", (object?)row.GirenMiktar ?? DBNull.Value);
                command.Parameters.AddWithValue($"@cikan{parameterPrefix}", (object?)row.CikanMiktar ?? DBNull.Value);
                command.Parameters.AddWithValue($"@stok{parameterPrefix}", row.SistemStok!.Value);
                command.Parameters.AddWithValue($"@birim{parameterPrefix}", (object?)row.Birim ?? DBNull.Value);
                command.Parameters.AddWithValue($"@alis{parameterPrefix}", (object?)row.AlisFiyati ?? DBNull.Value);
                command.Parameters.AddWithValue($"@satis{parameterPrefix}", (object?)row.SatisFiyati ?? DBNull.Value);
                command.Parameters.AddWithValue($"@pCinsi{parameterPrefix}", (object?)row.PCinsi ?? DBNull.Value);
            }

            command.CommandText =
                "INSERT INTO stok_sayim_sistem_stok " +
                "(SayimId, StokKodu, ReferansKodu, Barkod, UrunAdi, GirenMiktar, CikanMiktar, SistemStok, " +
                "Birim, AlisFiyati, SatisFiyati, PCinsi) VALUES " +
                string.Join(", ", values);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        cache.Remove(GetCacheKey(token));
        cache.Remove(GetOwnerCacheKey(sayimId, kullanici));
        return preview.ValidRows.Count;
    }

    private CachedPreview? GetOwnedPreview(int sayimId, string token, string kullanici)
    {
        if (string.IsNullOrWhiteSpace(token) || !cache.TryGetValue(GetCacheKey(token), out CachedPreview? preview))
            return null;

        return preview != null &&
            preview.SayimId == sayimId &&
            string.Equals(preview.Kullanici, kullanici, StringComparison.Ordinal)
                ? preview
                : null;
    }

    private static SistemStokOnizlemeViewModel ToViewModel(string token, CachedPreview preview) => new()
    {
        SayimId = preview.SayimId,
        Token = token,
        DosyaAdi = preview.FileName,
        ToplamSatirSayisi = preview.ValidRows.Count + preview.InvalidRows.Count,
        GecerliSatirSayisi = preview.ValidRows.Count,
        HataliSatirSayisi = preview.InvalidRows.Count,
        GecerliSatirlar = preview.ValidRows.Take(OrnekSatirSayisi).ToList(),
        HataliSatirlar = preview.InvalidRows.Take(OrnekSatirSayisi).ToList()
    };

    private static string GetCacheKey(string token) => $"StokSayim:SistemStokOnizleme:{token}";
    private static string GetOwnerCacheKey(int sayimId, string kullanici) =>
        $"StokSayim:SistemStokOnizlemeKullanici:{sayimId}:{kullanici}";

    private static int FindHeaderColumn(IXLWorksheet worksheet, int lastColumn, params string[] expectedHeaders)
    {
        for (var column = 1; column <= lastColumn; column++)
        {
            var header = Regex.Replace(worksheet.Cell(1, column).GetString().Trim(), @"\s+", " ");
            if (expectedHeaders.Any(expected =>
                    string.Equals(header, expected, StringComparison.OrdinalIgnoreCase)))
                return column;
        }
        return 0;
    }

    private static string? ReadOptionalText(
        IXLWorksheet worksheet,
        int row,
        int column,
        int maximumLength,
        string label,
        List<string> errors)
    {
        if (column == 0)
            return null;

        var value = worksheet.Cell(row, column).GetFormattedString().Trim();
        if (value.Length == 0)
            return null;
        if (value.Length > maximumLength)
            errors.Add($"{label} en fazla {maximumLength} karakter olabilir.");
        return value;
    }

    private static decimal? ReadOptionalDecimal(
        IXLWorksheet worksheet,
        int row,
        int column,
        int decimalPlaces,
        decimal maximumValue,
        string label,
        List<string> errors,
        bool allowNegative = false)
    {
        if (column == 0)
            return null;

        var cell = worksheet.Cell(row, column);
        if (cell.IsEmpty())
            return null;

        decimal value;
        var parsed = cell.DataType switch
        {
            XLDataType.Number => decimal.TryParse(
                cell.Value.ToString(CultureInfo.InvariantCulture),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value),
            XLDataType.Text => TryParseLocalizedDecimal(cell.GetString().Trim(), out value),
            _ => SetDefault(out value)
        };
        var belowMinimum = allowNegative ? value < -maximumValue : value < 0;
        if (!parsed || belowMinimum || value > maximumValue || decimal.Round(value, decimalPlaces) != value)
        {
            errors.Add(allowNegative
                ? $"{label} negatif veya pozitif olabilir, en fazla {decimalPlaces} ondalık basamaklı bir sayı olmalıdır."
                : $"{label} 0 veya daha büyük, en fazla {decimalPlaces} ondalık basamaklı bir sayı olmalıdır.");
            return null;
        }

        return value;
    }

    private static bool SetDefault(out decimal value)
    {
        value = 0;
        return false;
    }

    private static string ReadBarcode(IXLCell cell, out string error)
    {
        error = string.Empty;
        if (cell.IsEmpty())
            return string.Empty;

        if (cell.DataType == XLDataType.Text)
        {
            var text = cell.GetString().Trim();
            if (text.Length > 100)
                error = "Barkod en fazla 100 karakter olabilir.";
            else if (Regex.IsMatch(text, @"^\d+(?:[.,]\d+)?[Ee][+-]?\d+$"))
                error = "Barkod bilimsel gösterimde olamaz; Excel hücresini metin biçiminde girin.";
            return text;
        }

        if (cell.DataType != XLDataType.Number)
        {
            error = "Barkod metin veya sayısal hücre olmalıdır.";
            return string.Empty;
        }

        var numericValue = cell.GetDouble();
        if (!double.IsFinite(numericValue) || numericValue < 0 ||
            numericValue > 999999999999999d || numericValue != Math.Truncate(numericValue))
        {
            error = "Sayısal barkod sıfır veya daha büyük, tam ve en fazla 15 basamaklı olmalıdır.";
            return string.Empty;
        }

        var barcode = Convert.ToInt64(numericValue)
            .ToString(CultureInfo.InvariantCulture);
        var format = cell.Style.NumberFormat.Format;
        if (Regex.IsMatch(format, @"^0+$") && format.Length > barcode.Length)
            barcode = barcode.PadLeft(format.Length, '0');
        return barcode;
    }

    private static bool TryReadStock(IXLCell cell, out decimal stock)
    {
        stock = 0;
        if (cell.IsEmpty())
            return false;

        if (cell.DataType == XLDataType.Number)
        {
            var rawValue = cell.Value.ToString(CultureInfo.InvariantCulture);
            if (!decimal.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out stock))
                return false;
        }
        else if (cell.DataType == XLDataType.Text)
        {
            var rawValue = cell.GetString().Trim();
            if (!TryParseLocalizedDecimal(rawValue, out stock))
                return false;
        }
        else
        {
            return false;
        }

        return stock >= -999999999999999.999m &&
            stock <= 999999999999999.999m &&
            decimal.Round(stock, 3) == stock;
    }

    private static bool TryParseLocalizedDecimal(string value, out decimal number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace))
            return false;

        var commaIndex = value.LastIndexOf(',');
        var dotIndex = value.LastIndexOf('.');
        if (commaIndex >= 0 && dotIndex >= 0)
        {
            var decimalSeparator = commaIndex > dotIndex ? ',' : '.';
            var groupSeparator = decimalSeparator == ',' ? '.' : ',';
            value = value.Replace(groupSeparator.ToString(), string.Empty, StringComparison.Ordinal);
            if (value.Count(ch => ch == decimalSeparator) != 1)
                return false;
            value = decimalSeparator == ',' ? value.Replace(',', '.') : value;
        }
        else if (commaIndex >= 0)
        {
            if (value.Count(ch => ch == ',') != 1)
                return false;
            value = value.Replace(',', '.');
        }
        else if (dotIndex >= 0 && value.Count(ch => ch == '.') != 1)
        {
            return false;
        }

        return decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);
    }

    private sealed record CachedPreview(
        int SayimId,
        string Kullanici,
        string FileName,
        List<SistemStokSatiriOnizlemeViewModel> ValidRows,
        List<SistemStokSatiriOnizlemeViewModel> InvalidRows);
}
