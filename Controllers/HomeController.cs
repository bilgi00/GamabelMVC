using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using gamabelmvc.Models;
using gamabelmvc.Services;
using MySqlConnector;

namespace gamabelmvc.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly DbConnectionFactory _dbFactory;

    public HomeController(ILogger<HomeController> logger, DbConnectionFactory dbFactory)
    {
        _logger = logger;
        _dbFactory = dbFactory;
    }

    public async Task<IActionResult> Index()
    {
        var activeModule = HttpContext.Session.GetString("ActiveModule");

        if (activeModule == "Personel")
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
                return RedirectToAction("Login", "Account");

            return View("~/Views/Home/Index.cshtml");
        }

        if (activeModule != "STS")
            return View("~/Views/Home/Index.cshtml");

        var kullaniciId = HttpContext.Session.GetInt32("KullaniciId");
        if (kullaniciId == null)
            return RedirectToAction("StsLogin", "Account");

        // Dashboard özet kartları
        var dashboardData = new Dictionary<string, object>();
        dashboardData["ToplamEksik"] = 0;
        dashboardData["BekliyenEksik"] = 0;
        dashboardData["SevkEdilen"] = 0;
        dashboardData["AcilEksik"] = 0;
        dashboardData["EksikVerenSube"] = 0;
        dashboardData["KritikGeciken"] = 0;
        dashboardData["OnayBekleyenSevkiyat"] = 0;
        dashboardData["TekrarliUrun"] = 0;
        var subeId = HttpContext.Session.GetInt32("SubeId") ?? 0;
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        var haftaNo = GetHaftaNo();

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureBildirimTableAsync(conn);

                if (rol == "SubePersoneli")
                {
                    // Şube personeli - kendi eksikleri
                    using (var cmd = new MySqlCommand(@"
                        SELECT COUNT(*) as ToplamEksik, 
                               SUM(CASE WHEN Durum = 'Bekliyor' THEN 1 ELSE 0 END) as BekliyenEksik,
                               SUM(CASE WHEN Durum = 'SevkEdildi' THEN 1 ELSE 0 END) as SevkEdilen,
                               SUM(CASE WHEN AcilMi = true THEN 1 ELSE 0 END) as AcilEksik
                        FROM stk_EksikKaydi WHERE SubeId = @SubeId AND HaftaNo = @HaftaNo", conn))
                    {
                        cmd.Parameters.AddWithValue("@SubeId", subeId);
                        cmd.Parameters.AddWithValue("@HaftaNo", haftaNo);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                dashboardData["ToplamEksik"] = reader["ToplamEksik"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ToplamEksik"]);
                                dashboardData["BekliyenEksik"] = reader["BekliyenEksik"] == DBNull.Value ? 0 : Convert.ToInt32(reader["BekliyenEksik"]);
                                dashboardData["SevkEdilen"] = reader["SevkEdilen"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SevkEdilen"]);
                                dashboardData["AcilEksik"] = reader["AcilEksik"] == DBNull.Value ? 0 : Convert.ToInt32(reader["AcilEksik"]);
                            }
                        }
                    }

                    using (var cmd = new MySqlCommand(@"
                        SELECT COUNT(*)
                        FROM stk_EksikKaydi
                        WHERE SubeId = @SubeId AND Durum = 'Bekliyor' AND GeciktiMi = true", conn))
                    {
                        cmd.Parameters.AddWithValue("@SubeId", subeId);
                        dashboardData["KritikGeciken"] = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
                    }

                    using (var cmd = new MySqlCommand(@"
                        SELECT COUNT(*)
                        FROM stk_Sevkiyat sv
                        JOIN stk_EksikKaydi e ON sv.EksikKaydiId = e.Id
                        WHERE e.SubeId = @SubeId AND sv.Durum = 'Yolda'", conn))
                    {
                        cmd.Parameters.AddWithValue("@SubeId", subeId);
                        dashboardData["OnayBekleyenSevkiyat"] = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
                    }

                    using (var cmd = new MySqlCommand(@"
                        SELECT COUNT(*) FROM (
                            SELECT UrunId
                            FROM stk_EksikKaydi
                            WHERE SubeId = @SubeId AND GirisTarihi >= DATE_SUB(NOW(), INTERVAL 21 DAY)
                            GROUP BY UrunId
                            HAVING COUNT(DISTINCT HaftaNo) >= 3
                        ) t", conn))
                    {
                        cmd.Parameters.AddWithValue("@SubeId", subeId);
                        dashboardData["TekrarliUrun"] = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
                    }
                }
                else if (rol == "DepoSorumlusu" || rol == "Admin")
                {
                    // Depo sorumlusu - tüm eksikler
                    using (var cmd = new MySqlCommand(@"
                        SELECT COUNT(*) as ToplamEksik,
                               SUM(CASE WHEN Durum = 'Bekliyor' THEN 1 ELSE 0 END) as BekliyenEksik,
                               SUM(CASE WHEN Durum = 'SevkEdildi' THEN 1 ELSE 0 END) as SevkEdilen,
                               COUNT(DISTINCT SubeId) as EksikVerenSube
                        FROM stk_EksikKaydi WHERE HaftaNo = @HaftaNo", conn))
                    {
                        cmd.Parameters.AddWithValue("@HaftaNo", haftaNo);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                dashboardData["ToplamEksik"] = reader["ToplamEksik"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ToplamEksik"]);
                                dashboardData["BekliyenEksik"] = reader["BekliyenEksik"] == DBNull.Value ? 0 : Convert.ToInt32(reader["BekliyenEksik"]);
                                dashboardData["SevkEdilen"] = reader["SevkEdilen"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SevkEdilen"]);
                                dashboardData["EksikVerenSube"] = reader["EksikVerenSube"] == DBNull.Value ? 0 : Convert.ToInt32(reader["EksikVerenSube"]);
                            }
                        }
                    }

                    using (var cmd = new MySqlCommand(@"
                        SELECT COUNT(*)
                        FROM stk_EksikKaydi
                        WHERE Durum = 'Bekliyor' AND GeciktiMi = true", conn))
                    {
                        dashboardData["KritikGeciken"] = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
                    }

                    using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM stk_Sevkiyat WHERE Durum = 'Yolda'", conn))
                    {
                        dashboardData["OnayBekleyenSevkiyat"] = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
                    }

                    using (var cmd = new MySqlCommand(@"
                        SELECT COUNT(*) FROM (
                            SELECT UrunId
                            FROM stk_EksikKaydi
                            WHERE GirisTarihi >= DATE_SUB(NOW(), INTERVAL 21 DAY)
                            GROUP BY UrunId
                            HAVING COUNT(DISTINCT HaftaNo) >= 3
                        ) t", conn))
                    {
                        dashboardData["TekrarliUrun"] = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
                    }
                }

                var bildirimler = new List<dynamic>();
                using (var cmd = new MySqlCommand(@"
                    SELECT Id, Baslik, Mesaj, Link, OkunduMu, OlusturmaTarihi
                    FROM stk_Bildirim
                    WHERE (HedefRol = @Rol OR (HedefRol = 'SubePersoneli' AND HedefSubeId = @SubeId))
                    ORDER BY OlusturmaTarihi DESC
                    LIMIT 10", conn))
                {
                    cmd.Parameters.AddWithValue("@Rol", rol);
                    cmd.Parameters.AddWithValue("@SubeId", subeId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            bildirimler.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                Baslik = reader.GetString("Baslik"),
                                Mesaj = reader.GetString("Mesaj"),
                                Link = reader["Link"] == DBNull.Value ? string.Empty : reader["Link"]?.ToString() ?? string.Empty,
                                OkunduMu = reader.GetBoolean("OkunduMu"),
                                OlusturmaTarihi = Convert.ToDateTime(reader["OlusturmaTarihi"])
                            });
                        }
                    }
                }
                dashboardData["Bildirimler"] = bildirimler;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard veri yükleme hatası");
        }

        ViewBag.DashboardData = dashboardData;
        ViewBag.HaftaNo = haftaNo;
        return View("~/Views/STS/Home/Index.cshtml");
    }

    [HttpPost]
    public async Task<IActionResult> BildirimleriOkunduYap(string ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
            return Ok();

        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var parsed) ? parsed : 0)
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        if (idList.Count == 0)
            return Ok();

        using (var conn = await _dbFactory.CreateConnectionAsync())
        {
            await EnsureBildirimTableAsync(conn);
            var placeholders = string.Join(",", idList.Select((_, i) => $"@p{i}"));
            using (var cmd = new MySqlCommand($"UPDATE stk_Bildirim SET OkunduMu = true WHERE Id IN ({placeholders})", conn))
            {
                for (int i = 0; i < idList.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@p{i}", idList[i]);
                }
                await cmd.ExecuteNonQueryAsync();
            }
        }

        return Ok();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private string GetHaftaNo()
    {
        var today = DateTime.Now;
        var cultureInfo = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var weekOfYear = cultureInfo.Calendar.GetWeekOfYear(today, System.Globalization.CalendarWeekRule.FirstFullWeek, DayOfWeek.Monday);
        return $"{today.Year}-W{weekOfYear:D2}";
    }

    private async Task EnsureBildirimTableAsync(MySqlConnection conn)
    {
        var tableQuery = @"
            CREATE TABLE IF NOT EXISTS stk_Bildirim (
                Id INT PRIMARY KEY AUTO_INCREMENT,
                HedefRol VARCHAR(50) NOT NULL,
                HedefSubeId INT NULL,
                Baslik VARCHAR(150) NOT NULL,
                Mesaj VARCHAR(500) NOT NULL,
                Link VARCHAR(250) NULL,
                OkunduMu BOOLEAN NOT NULL DEFAULT FALSE,
                OlusturmaTarihi DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX idx_rol_okundu (HedefRol, OkunduMu),
                INDEX idx_sube (HedefSubeId)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;";

        using (var cmd = new MySqlCommand(tableQuery, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
