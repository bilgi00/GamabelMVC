using System.Globalization;
using gamabelmvc.Models.PRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace gamabelmvc.Controllers.PRS;

[Authorize(Policy = "PrsAdmin")]
public class MesaiYonetimController : Controller
{
    private const decimal MaxDecimal10Scale2 = 99999999.99m;
    private readonly string _connectionString;

    public MesaiYonetimController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MyConnection")!;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsAdmin())
            return Forbid();

        var model = new MesaiSaatUcretYonetimViewModel
        {
            BasariMesaji = TempData["BasariMesaji"] as string,
            HataMesaji = TempData["HataMesaji"] as string
        };

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(
                @"SELECT id, net_asgari_ucret, brut_asgari_ucret, gunluk_brut,
                         haftalik_brut, saatlik_brut
                  FROM mesaisaat
                  ORDER BY id DESC
                  LIMIT 1", connection);
            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                model.KayitId = reader.GetInt32(0);
                model.NetAsgariUcret = ReadNullableDecimal(reader, 1);
                model.BrutAsgariUcret = ReadNullableDecimal(reader, 2);
                model.GunlukBrut = ReadNullableDecimal(reader, 3);
                model.HaftalikBrut = ReadNullableDecimal(reader, 4);
                model.SaatlikBrut = ReadNullableDecimal(reader, 5);
            }
        }
        catch
        {
            model.HataMesaji = "Mesaisaat verileri alınamadı. Veritabanı bağlantısını ve alan adlarını kontrol edin.";
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guncelle(MesaiSaatUcretYonetimViewModel model)
    {
        if (!IsAdmin())
            return Forbid();

        if (!ModelState.IsValid || !IsValidAmount(model.NetAsgariUcret) ||
            !IsValidAmount(model.BrutAsgariUcret) || !IsValidAmount(model.GunlukBrut) ||
            !IsValidAmount(model.HaftalikBrut) || !IsValidAmount(model.SaatlikBrut))
        {
            TempData["HataMesaji"] = "Tutarlar DECIMAL(10,2) sınırları içinde olmalıdır.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var isInsert = !model.KayitId.HasValue;
            var sql = isInsert
                ? @"INSERT INTO mesaisaat
                    (net_asgari_ucret, brut_asgari_ucret, gunluk_brut, haftalik_brut, saatlik_brut)
                    VALUES (@net, @brut, @gunluk, @haftalik, @saatlik)"
                : @"UPDATE mesaisaat
                    SET net_asgari_ucret = @net,
                        brut_asgari_ucret = @brut,
                        gunluk_brut = @gunluk,
                        haftalik_brut = @haftalik,
                        saatlik_brut = @saatlik
                    WHERE id = @id";

            await using var command = new MySqlCommand(sql, connection);
            AddNullableDecimal(command, "@net", model.NetAsgariUcret);
            AddNullableDecimal(command, "@brut", model.BrutAsgariUcret);
            AddNullableDecimal(command, "@gunluk", model.GunlukBrut);
            AddNullableDecimal(command, "@haftalik", model.HaftalikBrut);
            AddNullableDecimal(command, "@saatlik", model.SaatlikBrut);
            if (!isInsert)
                command.Parameters.AddWithValue("@id", model.KayitId!.Value);

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (!isInsert && affectedRows == 0)
            {
                await using var existsCommand = new MySqlCommand(
                    "SELECT COUNT(*) FROM mesaisaat WHERE id = @id", connection);
                existsCommand.Parameters.AddWithValue("@id", model.KayitId!.Value);
                if (Convert.ToInt32(await existsCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 0)
                {
                    TempData["HataMesaji"] = "Güncellenecek mesaisaat kaydı bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }
            }

            TempData["BasariMesaji"] = "Mesaisaat değerleri güncellendi.";
        }
        catch
        {
            TempData["HataMesaji"] = "Mesaisaat değerleri güncellenemedi.";
        }

        return RedirectToAction(nameof(Index));
    }

    private bool IsAdmin() =>
        string.Equals(HttpContext.Session.GetString("Rol")?.Trim(), "admin", StringComparison.OrdinalIgnoreCase);

    private static decimal? ReadNullableDecimal(MySqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);

    private static bool IsValidAmount(decimal? value) =>
        !value.HasValue || (value.Value >= 0 && value.Value <= MaxDecimal10Scale2 &&
                            decimal.Round(value.Value, 2) == value.Value);

    private static void AddNullableDecimal(MySqlCommand command, string name, decimal? value) =>
        command.Parameters.AddWithValue(name, (object?)value ?? DBNull.Value);
}