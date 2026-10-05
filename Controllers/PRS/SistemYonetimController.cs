using gamabelmvc.Models.PRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace gamabelmvc.Controllers.PRS;

[Authorize(Policy = "PrsAdmin")]
public sealed class SistemYonetimController : Controller
{
    private readonly string _connectionString;
    private readonly ILogger<SistemYonetimController> _logger;

    public SistemYonetimController(
        IConfiguration configuration,
        ILogger<SistemYonetimController> logger)
    {
        _connectionString = configuration.GetConnectionString("MyConnection")
            ?? throw new InvalidOperationException("MyConnection veritabanı bağlantısı yapılandırılmamış.");
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Birimler()
    {
        var model = new BirimYonetimViewModel
        {
            YeniBirimAdi = string.Empty
        };

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(
                "SELECT id, birim_adi FROM birimler ORDER BY birim_adi, id",
                connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                model.Birimler.Add(new BirimYonetimSatiri
                {
                    Id = reader.GetInt32(0),
                    BirimAdi = reader.GetString(1)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Birim yönetimi için birimler yüklenemedi.");
            TempData["Hata"] = "Birimler yüklenemedi. Veritabanı bağlantısını ve tabloyu kontrol edin.";
        }

        return View("~/Views/PRS/SistemYonetim/Birimler.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BirimEkle(BirimYonetimViewModel model)
    {
        var name = model.YeniBirimAdi?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
        {
            TempData["Hata"] = "Birim adı boş olamaz ve 255 karakteri aşamaz.";
            return RedirectToAction(nameof(Birimler));
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            if (await BirimVarMiAsync(connection, transaction, name))
            {
                await transaction.RollbackAsync();
                TempData["Hata"] = "Bu birim adı zaten kayıtlı.";
                return RedirectToAction(nameof(Birimler));
            }

            await using var command = new MySqlCommand(
                "INSERT INTO birimler (birim_adi) VALUES (@birimAdi)",
                connection,
                transaction);
            command.Parameters.Add("@birimAdi", MySqlDbType.VarChar, 255).Value = name;
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();

            TempData["Basari"] = $"“{name}” birimi eklendi.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yeni birim eklenemedi.");
            TempData["Hata"] = "Birim eklenemedi. Veritabanı hatasını kontrol edin.";
        }

        return RedirectToAction(nameof(Birimler));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BirimGuncelle(BirimYonetimSatiri model)
    {
        var name = model.BirimAdi?.Trim() ?? string.Empty;
        if (model.Id <= 0 || string.IsNullOrWhiteSpace(name) || name.Length > 255)
        {
            TempData["Hata"] = "Geçerli bir birim ve 255 karakteri aşmayan bir ad girin.";
            return RedirectToAction(nameof(Birimler));
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            string? oldName;
            await using (var currentUnitCommand = new MySqlCommand(
                "SELECT birim_adi FROM birimler WHERE id = @id FOR UPDATE",
                connection,
                transaction))
            {
                currentUnitCommand.Parameters.Add("@id", MySqlDbType.Int32).Value = model.Id;
                var currentName = await currentUnitCommand.ExecuteScalarAsync();
                if (currentName is null || currentName == DBNull.Value)
                {
                    await transaction.RollbackAsync();
                    TempData["Hata"] = "Güncellenecek birim bulunamadı.";
                    return RedirectToAction(nameof(Birimler));
                }

                oldName = Convert.ToString(currentName);
            }

            await using (var duplicateCommand = new MySqlCommand(
                "SELECT COUNT(*) FROM birimler WHERE LOWER(TRIM(birim_adi)) = LOWER(TRIM(@birimAdi)) AND id <> @id",
                connection,
                transaction))
            {
                duplicateCommand.Parameters.Add("@birimAdi", MySqlDbType.VarChar, 255).Value = name;
                duplicateCommand.Parameters.Add("@id", MySqlDbType.Int32).Value = model.Id;
                if (Convert.ToInt32(await duplicateCommand.ExecuteScalarAsync()) > 0)
                {
                    await transaction.RollbackAsync();
                    TempData["Hata"] = "Bu birim adı başka bir kayıtta kullanılıyor.";
                    return RedirectToAction(nameof(Birimler));
                }
            }

            await using (var updateUnit = new MySqlCommand(
                "UPDATE birimler SET birim_adi = @birimAdi WHERE id = @id",
                connection,
                transaction))
            {
                updateUnit.Parameters.Add("@birimAdi", MySqlDbType.VarChar, 255).Value = name;
                updateUnit.Parameters.Add("@id", MySqlDbType.Int32).Value = model.Id;
                await updateUnit.ExecuteNonQueryAsync();
            }

            await UpdateBirimReferencesAsync(connection, transaction, "personeller", "birim_adi", oldName, name);
            await UpdateBirimReferencesAsync(connection, transaction, "admin_kullanicilar", "birim", oldName, name);

            await transaction.CommitAsync();
            TempData["Basari"] = "Birim adı ve bağlı personel/kullanıcı birim bilgileri güncellendi.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Birim güncellenemedi. Birim ID: {BirimId}", model.Id);
            TempData["Hata"] = "Birim güncellenemedi. Veritabanı hatasını kontrol edin.";
        }

        return RedirectToAction(nameof(Birimler));
    }

    [HttpGet]
    public async Task<IActionResult> Personeller(string? arama)
    {
        var model = new PersonelYonetimViewModel
        {
            Arama = arama?.Trim() ?? string.Empty
        };

        if (model.Arama.Length > 100)
        {
            TempData["Hata"] = "Arama metni en fazla 100 karakter olabilir.";
            return RedirectToAction(nameof(Personeller));
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using (var unitCommand = new MySqlCommand(
                "SELECT birim_adi FROM birimler ORDER BY birim_adi",
                connection))
            await using (var unitReader = await unitCommand.ExecuteReaderAsync())
            {
                while (await unitReader.ReadAsync())
                    model.Birimler.Add(unitReader.GetString(0));
            }

            await using var command = new MySqlCommand(
                @"SELECT per_no, ad, soyad, birim_adi, per_statu
                  FROM personeller
                  WHERE @arama = ''
                     OR per_no LIKE @aramaPattern
                     OR COALESCE(ad, '') LIKE @aramaPattern
                     OR COALESCE(soyad, '') LIKE @aramaPattern
                     OR COALESCE(birim_adi, '') LIKE @aramaPattern
                     OR COALESCE(per_statu, '') LIKE @aramaPattern
                  ORDER BY birim_adi, ad, soyad, per_no",
                connection);
            command.Parameters.Add("@arama", MySqlDbType.VarChar, 100).Value = model.Arama;
            command.Parameters.Add("@aramaPattern", MySqlDbType.VarChar, 202).Value = $"%{model.Arama}%";
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                model.Personeller.Add(new PersonelYonetimSatiri
                {
                    PerNo = reader.GetString(0),
                    Ad = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Soyad = reader.IsDBNull(2) ? null : reader.GetString(2),
                    BirimAdi = reader.IsDBNull(3) ? null : reader.GetString(3),
                    PerStatu = reader.IsDBNull(4) ? null : reader.GetString(4)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Personel yönetimi için kayıtlar yüklenemedi.");
            TempData["Hata"] = "Personel kayıtları yüklenemedi. Veritabanı bağlantısını ve tabloyu kontrol edin.";
        }

        return View("~/Views/PRS/SistemYonetim/Personeller.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PersonelGuncelle(PersonelYonetimSatiri model, string? arama)
    {
        model.PerNo = model.PerNo?.Trim() ?? string.Empty;
        model.Ad = Normalize(model.Ad);
        model.Soyad = Normalize(model.Soyad);
        model.BirimAdi = Normalize(model.BirimAdi);
        model.PerStatu = Normalize(model.PerStatu);

        if (string.IsNullOrWhiteSpace(model.PerNo)
            || model.PerNo.Length > 50
            || model.Ad?.Length > 100
            || model.Soyad?.Length > 100
            || model.BirimAdi?.Length > 255
            || model.PerStatu?.Length > 100)
        {
            TempData["Hata"] = "Personel alanlarından biri geçersiz veya izin verilen uzunluğu aşıyor.";
            return RedirectToAction(nameof(Personeller), new { arama });
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            if (!string.IsNullOrEmpty(model.BirimAdi))
            {
                await using var unitCommand = new MySqlCommand(
                    "SELECT COUNT(*) FROM birimler WHERE birim_adi = @birimAdi",
                    connection);
                unitCommand.Parameters.Add("@birimAdi", MySqlDbType.VarChar, 255).Value = model.BirimAdi;
                if (Convert.ToInt32(await unitCommand.ExecuteScalarAsync()) == 0)
                {
                    TempData["Hata"] = "Seçilen birim bulunamadı. Önce birimi kaydedin.";
                    return RedirectToAction(nameof(Personeller), new { arama });
                }
            }

            await using var command = new MySqlCommand(
                @"UPDATE personeller
                  SET ad = @ad, soyad = @soyad, birim_adi = @birimAdi, per_statu = @perStatu
                  WHERE per_no = @perNo",
                connection);
            command.Parameters.Add("@ad", MySqlDbType.VarChar, 100).Value = (object?)model.Ad ?? DBNull.Value;
            command.Parameters.Add("@soyad", MySqlDbType.VarChar, 100).Value = (object?)model.Soyad ?? DBNull.Value;
            command.Parameters.Add("@birimAdi", MySqlDbType.VarChar, 255).Value = (object?)model.BirimAdi ?? DBNull.Value;
            command.Parameters.Add("@perStatu", MySqlDbType.VarChar, 100).Value = (object?)model.PerStatu ?? DBNull.Value;
            command.Parameters.Add("@perNo", MySqlDbType.VarChar, 50).Value = model.PerNo;
            var affectedRows = await command.ExecuteNonQueryAsync();

            if (affectedRows == 0)
            {
                await using var existsCommand = new MySqlCommand(
                    "SELECT COUNT(*) FROM personeller WHERE per_no = @perNo",
                    connection);
                existsCommand.Parameters.Add("@perNo", MySqlDbType.VarChar, 50).Value = model.PerNo;
                if (Convert.ToInt32(await existsCommand.ExecuteScalarAsync()) == 0)
                {
                    TempData["Hata"] = "Güncellenecek personel bulunamadı.";
                    return RedirectToAction(nameof(Personeller), new { arama });
                }
            }

            TempData["Basari"] = $"{model.PerNo} numaralı personel güncellendi.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Personel güncellenemedi. Personel No: {PerNo}", model.PerNo);
            TempData["Hata"] = "Personel güncellenemedi. Veritabanı hatasını kontrol edin.";
        }

        return RedirectToAction(nameof(Personeller), new { arama });
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<bool> BirimVarMiAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string name)
    {
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM birimler WHERE LOWER(TRIM(birim_adi)) = LOWER(TRIM(@birimAdi))",
            connection,
            transaction);
        command.Parameters.Add("@birimAdi", MySqlDbType.VarChar, 255).Value = name;
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task UpdateBirimReferencesAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string table,
        string column,
        string? oldName,
        string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName))
            return;

        var allowed = (table, column) switch
        {
            ("personeller", "birim_adi") => true,
            ("admin_kullanicilar", "birim") => true,
            _ => false
        };
        if (!allowed)
            throw new InvalidOperationException("Birim referans tablosu geçersiz.");

        await using var command = new MySqlCommand(
            $"UPDATE `{table}` SET `{column}` = @newName WHERE `{column}` = @oldName",
            connection,
            transaction);
        command.Parameters.Add("@newName", MySqlDbType.VarChar, 255).Value = newName;
        command.Parameters.Add("@oldName", MySqlDbType.VarChar, 255).Value = oldName.Trim();
        await command.ExecuteNonQueryAsync();
    }
}
