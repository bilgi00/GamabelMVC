using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace gamabelmvc.Controllers.PRS;

public class ResmiTatilController : Controller
{
    private readonly string _connectionString;

    public ResmiTatilController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MyConnection")!;
    }

    public async Task<IActionResult> Index()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return RedirectToAction("Login", "Account");

        var tatiller = new List<Dictionary<string, object?>>();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(
                "SELECT id, tatil_adi, tatil_tarihi, tatil_turu, gun_adı, kacinci_gun, aciklama FROM kktc_resmi_tatiller ORDER BY tatil_tarihi",
                connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tatiller.Add(new Dictionary<string, object?>
                {
                    ["Id"] = reader.GetInt32(0),
                    ["TatilAdi"] = reader.GetString(1),
                    ["TatilTarihi"] = reader.GetDateTime(2),
                    ["TatilTuru"] = reader.GetString(3),
                    ["GunAdi"] = reader.GetString(4),
                    ["KacinciGun"] = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ["Aciklama"] = reader.IsDBNull(6) ? null : reader.GetString(6)
                });
            }
        }
        catch (Exception ex)
        {
            ViewBag.Hata = "Veritabanı hatası: " + ex.Message;
        }

        ViewBag.KullaniciAdi = HttpContext.Session.GetString("KullaniciAdi");
        return View(tatiller);
    }
}
