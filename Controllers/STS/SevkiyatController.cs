using Microsoft.AspNetCore.Mvc;
using gamabelmvc.Models.STS;
using gamabelmvc.Services;
using MySqlConnector;
using OfficeOpenXml;

namespace gamabelmvc.Controllers.STS;

public class SevkiyatController : Controller
{
    private readonly DbConnectionFactory _dbFactory;

    public SevkiyatController(DbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // GET: Sevkiyat/Index - Sevk bekleyen eksikler + sevk edilenler + onay bekleyenler
    public async Task<IActionResult> Index(string? subeAdi = null, string? firma = null, string? grup = null, string? forceRefresh = null)
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        if (rol != "DepoSorumlusu" && rol != "Admin")
            return RedirectToAction("Index", "Home");

        // Parametreleri trim et
        subeAdi = subeAdi?.Trim();
        firma = firma?.Trim();
        grup = grup?.Trim();

        try
        {
            // Eğer forceRefresh geldi ise (Güncelle butonu basıldı), Session'ı güncelle
            if (forceRefresh == "true")
            {
                HttpContext.Session.SetString("LastSubeFilter", subeAdi ?? "");
                HttpContext.Session.SetString("LastFirmaFilter", firma ?? "");
                HttpContext.Session.SetString("LastGrupFilter", grup ?? "");
            }
            else if (string.IsNullOrEmpty(subeAdi) && string.IsNullOrEmpty(firma) && string.IsNullOrEmpty(grup))
            {
                // Eğer parametreler boş ise, Session'dan son filtreleri al
                subeAdi = HttpContext.Session.GetString("LastSubeFilter") ?? "";
                firma = HttpContext.Session.GetString("LastFirmaFilter") ?? "";
                grup = HttpContext.Session.GetString("LastGrupFilter") ?? "";
            }

            // Boş string'leri null'a dönüştür (SQL query için)
            var subeAdiForQuery = string.IsNullOrWhiteSpace(subeAdi) ? null : subeAdi;
            var firmaForQuery = string.IsNullOrWhiteSpace(firma) ? null : firma;
            var grupForQuery = string.IsNullOrWhiteSpace(grup) ? null : grup;

            var bekleyenEksikler = new List<dynamic>();
            var sevkiyatlar = new List<dynamic>();
            var subeler = new List<dynamic>();
            var firmalar = new List<dynamic>();
            var gruplar = new List<dynamic>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureSiparisLinkColumnAsync(conn);
                await EnsureEksikKaydiSilColumnAsync(conn);

                // Tüm bölümleri al
                var subeQuery = "SELECT DISTINCT Ad FROM stk_Sube WHERE AktifMi = true ORDER BY Ad";
                using (var subeCmd = new MySqlCommand(subeQuery, conn))
                using (var reader = await subeCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        subeler.Add(new { Ad = reader.GetString("Ad") });
                    }
                }

                // Tüm firmaları al
                var firmaQuery = "SELECT DISTINCT Firma FROM stk_Urun WHERE Firma IS NOT NULL AND Firma != '' ORDER BY Firma";
                using (var firmaCmd = new MySqlCommand(firmaQuery, conn))
                using (var reader = await firmaCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        firmalar.Add(new { Firma = reader.GetString("Firma") });
                    }
                }

                // Tüm grupları al
                var grupQuery = "SELECT DISTINCT Grup FROM stk_Urun WHERE Grup IS NOT NULL AND Grup != '' ORDER BY Grup";
                using (var grupCmd = new MySqlCommand(grupQuery, conn))
                using (var reader = await grupCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        gruplar.Add(new { Grup = reader.GetString("Grup") });
                    }
                }

                var bekleyenQuery = @"
                    SELECT e.Id, e.SiparisNo, u.Id as UrunId, u.Ad as UrunAdi, s.Ad as SubeAdi, u.Firma, u.Grup, e.Miktar, e.AcilMi, e.GeciktiMi, e.GirisTarihi
                    FROM stk_EksikKaydi e
                    JOIN stk_Urun u ON e.UrunId = u.Id
                    JOIN stk_Sube s ON e.SubeId = s.Id
                    WHERE e.Durum = 'Bekliyor' AND e.SilindiMi = FALSE";

                // Bölüm filtrelemesi
                if (!string.IsNullOrWhiteSpace(subeAdiForQuery))
                {
                    bekleyenQuery += " AND s.Ad = @SubeAdi";
                }

                // Firma filtrelemesi
                if (!string.IsNullOrWhiteSpace(firmaForQuery))
                {
                    bekleyenQuery += " AND u.Firma = @Firma";
                }

                // Grup filtrelemesi
                if (!string.IsNullOrWhiteSpace(grupForQuery))
                {
                    bekleyenQuery += " AND u.Grup = @Grup";
                }

                bekleyenQuery += " ORDER BY e.GirisTarihi ASC";

                using (var bekleyenCmd = new MySqlCommand(bekleyenQuery, conn))
                {
                    if (!string.IsNullOrWhiteSpace(subeAdiForQuery))
                    {
                        bekleyenCmd.Parameters.AddWithValue("@SubeAdi", subeAdiForQuery);
                    }
                    if (!string.IsNullOrWhiteSpace(firmaForQuery))
                    {
                        bekleyenCmd.Parameters.AddWithValue("@Firma", firmaForQuery);
                    }
                    if (!string.IsNullOrWhiteSpace(grupForQuery))
                    {
                        bekleyenCmd.Parameters.AddWithValue("@Grup", grupForQuery);
                    }

                    using (var reader = await bekleyenCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            bekleyenEksikler.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                SiparisNo = reader["SiparisNo"] == DBNull.Value ? string.Empty : reader["SiparisNo"]?.ToString() ?? string.Empty,
                                UrunId = reader.GetInt32("UrunId"),
                                UrunAdi = reader.GetString("UrunAdi"),
                                SubeAdi = reader.GetString("SubeAdi"),
                                Firma = reader["Firma"] == DBNull.Value ? string.Empty : reader["Firma"]?.ToString() ?? string.Empty,
                                Grup = reader["Grup"] == DBNull.Value ? string.Empty : reader["Grup"]?.ToString() ?? string.Empty,
                                Miktar = reader.GetDecimal("Miktar"),
                                AcilMi = reader["AcilMi"] != DBNull.Value && Convert.ToBoolean(reader["AcilMi"]),
                                GeciktiMi = reader["GeciktiMi"] != DBNull.Value && Convert.ToBoolean(reader["GeciktiMi"]),
                                GirisTarihi = Convert.ToDateTime(reader["GirisTarihi"])
                            });
                        }
                    }
                }

                var query = @"
                    SELECT sv.*, e.Id as EksikId, u.Ad as UrunAdi, s.Ad as SubeAdi, fs.Id as FabrikaSiparisId,
                           CASE WHEN sv.Durum = 'Yolda' AND TIMESTAMPDIFF(HOUR, sv.SevkTarihi, NOW()) > 48 THEN 1 ELSE 0 END as GecikmeUyarisi
                    FROM stk_Sevkiyat sv
                    JOIN stk_EksikKaydi e ON sv.EksikKaydiId = e.Id
                    JOIN stk_Urun u ON e.UrunId = u.Id
                    JOIN stk_Sube s ON e.SubeId = s.Id
                    LEFT JOIN stk_FabrikaSiparisi fs ON fs.KaynakSevkiyatId = sv.Id
                    WHERE sv.Durum <> 'Onaylandi'
                    ORDER BY sv.SevkTarihi DESC";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            sevkiyatlar.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                EksikId = reader.GetInt32("EksikId"),
                                UrunAdi = reader.GetString("UrunAdi"),
                                SubeAdi = reader.GetString("SubeAdi"),
                                SevkMiktari = reader.GetDecimal("SevkMiktari"),
                                Durum = reader.GetString("Durum"),
                                SevkTarihi = Convert.ToDateTime(reader["SevkTarihi"]),
                                SubeOnayTarihi = reader["SubeOnayTarihi"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["SubeOnayTarihi"]),
                                FabrikayaAktarildi = reader["FabrikaSiparisId"] != DBNull.Value,
                                GecikmeUyarisi = reader["GecikmeUyarisi"] != DBNull.Value && Convert.ToInt32(reader["GecikmeUyarisi"]) == 1,
                                TeslimSuresiGun = reader["SubeOnayTarihi"] == DBNull.Value
                                    ? (double?)null
                                    : Math.Round((Convert.ToDateTime(reader["SubeOnayTarihi"]) - Convert.ToDateTime(reader["SevkTarihi"])).TotalDays, 1)
                            });
                        }
                    }
                }
            }

            ViewBag.BekleyenEksikler = bekleyenEksikler;
            ViewBag.Subeler = subeler;
            ViewBag.SeciiliSube = subeAdi;
            ViewBag.Firmalar = firmalar;
            ViewBag.SeciliFirma = firma;
            ViewBag.Gruplar = gruplar;
            ViewBag.SeciliGrup = grup;

            return View("~/Views/STS/Sevkiyat/Index.cshtml", sevkiyatlar);
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // GET: Sevkiyat/SevkBekleyenEksikler - Sevk bekleyen eksikleri gösteren sayfa
    public async Task<IActionResult> SevkBekleyenEksikler(string? subeAdi = null, string? firma = null, string? grup = null, string? forceRefresh = null)
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        if (rol != "DepoSorumlusu" && rol != "Admin")
            return RedirectToAction("Index", "Home");

        // Parametreleri trim et
        subeAdi = subeAdi?.Trim();
        firma = firma?.Trim();
        grup = grup?.Trim();

        try
        {
            // Eğer forceRefresh geldi ise (Güncelle butonu basıldı), Session'ı güncelle
            if (forceRefresh == "true")
            {
                HttpContext.Session.SetString("LastSubeFilter", subeAdi ?? "");
                HttpContext.Session.SetString("LastFirmaFilter", firma ?? "");
                HttpContext.Session.SetString("LastGrupFilter", grup ?? "");
            }
            else if (string.IsNullOrEmpty(subeAdi) && string.IsNullOrEmpty(firma) && string.IsNullOrEmpty(grup))
            {
                // Eğer parametreler boş ise, Session'dan son filtreleri al
                subeAdi = HttpContext.Session.GetString("LastSubeFilter") ?? "";
                firma = HttpContext.Session.GetString("LastFirmaFilter") ?? "";
                grup = HttpContext.Session.GetString("LastGrupFilter") ?? "";
            }

            // Boş string'leri null'a dönüştür (SQL query için)
            var subeAdiForQuery = string.IsNullOrWhiteSpace(subeAdi) ? null : subeAdi;
            var firmaForQuery = string.IsNullOrWhiteSpace(firma) ? null : firma;
            var grupForQuery = string.IsNullOrWhiteSpace(grup) ? null : grup;

            var bekleyenEksikler = new List<dynamic>();
            var subeler = new List<dynamic>();
            var firmalar = new List<dynamic>();
            var gruplar = new List<dynamic>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureSiparisLinkColumnAsync(conn);
                await EnsureEksikKaydiSilColumnAsync(conn);

                // Tüm bölümleri al
                var subeQuery = "SELECT DISTINCT Ad FROM stk_Sube WHERE AktifMi = true ORDER BY Ad";
                using (var subeCmd = new MySqlCommand(subeQuery, conn))
                using (var reader = await subeCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        subeler.Add(new { Ad = reader.GetString("Ad") });
                    }
                }

                // Tüm firmaları al
                var firmaQuery = "SELECT DISTINCT Firma FROM stk_Urun WHERE Firma IS NOT NULL AND Firma != '' ORDER BY Firma";
                using (var firmaCmd = new MySqlCommand(firmaQuery, conn))
                using (var reader = await firmaCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        firmalar.Add(new { Firma = reader.GetString("Firma") });
                    }
                }

                // Tüm grupları al
                var grupQuery = "SELECT DISTINCT Grup FROM stk_Urun WHERE Grup IS NOT NULL AND Grup != '' ORDER BY Grup";
                using (var grupCmd = new MySqlCommand(grupQuery, conn))
                using (var reader = await grupCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        gruplar.Add(new { Grup = reader.GetString("Grup") });
                    }
                }

                var bekleyenQuery = @"
                    SELECT e.Id, e.SiparisNo, u.Id as UrunId, u.Ad as UrunAdi, s.Ad as SubeAdi, u.Firma, u.Grup, e.Miktar, e.AcilMi, e.GeciktiMi, e.GirisTarihi
                    FROM stk_EksikKaydi e
                    JOIN stk_Urun u ON e.UrunId = u.Id
                    JOIN stk_Sube s ON e.SubeId = s.Id
                    WHERE e.Durum = 'Bekliyor' AND e.SilindiMi = FALSE";

                // Bölüm filtrelemesi
                if (!string.IsNullOrWhiteSpace(subeAdiForQuery))
                {
                    bekleyenQuery += " AND s.Ad = @SubeAdi";
                }

                // Firma filtrelemesi
                if (!string.IsNullOrWhiteSpace(firmaForQuery))
                {
                    bekleyenQuery += " AND u.Firma = @Firma";
                }

                // Grup filtrelemesi
                if (!string.IsNullOrWhiteSpace(grupForQuery))
                {
                    bekleyenQuery += " AND u.Grup = @Grup";
                }

                bekleyenQuery += " ORDER BY e.GirisTarihi ASC";

                using (var bekleyenCmd = new MySqlCommand(bekleyenQuery, conn))
                {
                    if (!string.IsNullOrWhiteSpace(subeAdiForQuery))
                    {
                        bekleyenCmd.Parameters.AddWithValue("@SubeAdi", subeAdiForQuery);
                    }
                    if (!string.IsNullOrWhiteSpace(firmaForQuery))
                    {
                        bekleyenCmd.Parameters.AddWithValue("@Firma", firmaForQuery);
                    }
                    if (!string.IsNullOrWhiteSpace(grupForQuery))
                    {
                        bekleyenCmd.Parameters.AddWithValue("@Grup", grupForQuery);
                    }

                    using (var reader = await bekleyenCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            bekleyenEksikler.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                SiparisNo = reader["SiparisNo"] == DBNull.Value ? string.Empty : reader["SiparisNo"]?.ToString() ?? string.Empty,
                                UrunId = reader.GetInt32("UrunId"),
                                UrunAdi = reader.GetString("UrunAdi"),
                                SubeAdi = reader.GetString("SubeAdi"),
                                Firma = reader["Firma"] == DBNull.Value ? string.Empty : reader["Firma"]?.ToString() ?? string.Empty,
                                Grup = reader["Grup"] == DBNull.Value ? string.Empty : reader["Grup"]?.ToString() ?? string.Empty,
                                Miktar = reader.GetDecimal("Miktar"),
                                AcilMi = reader["AcilMi"] != DBNull.Value && Convert.ToBoolean(reader["AcilMi"]),
                                GeciktiMi = reader["GeciktiMi"] != DBNull.Value && Convert.ToBoolean(reader["GeciktiMi"]),
                                GirisTarihi = Convert.ToDateTime(reader["GirisTarihi"])
                            });
                        }
                    }
                }
            }

            ViewBag.BekleyenEksikler = bekleyenEksikler;
            ViewBag.Subeler = subeler;
            ViewBag.SeciiliSube = subeAdi;
            ViewBag.Firmalar = firmalar;
            ViewBag.SeciliFirma = firma;
            ViewBag.Gruplar = gruplar;
            ViewBag.SeciliGrup = grup;
            ViewBag.Rol = rol;
            ViewBag.IsAdmin = rol == "Admin";

            return View("~/Views/STS/Sevkiyat/SevkBekleyenEksikler.cshtml");
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // GET: Sevkiyat/Gecmis - Sevkiyat geçmişini gösteren sayfa
    public async Task<IActionResult> Gecmis(string? subeAdi = null, string? firma = null, string? grup = null, string? forceRefresh = null)
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        if (rol != "DepoSorumlusu" && rol != "Admin")
            return RedirectToAction("Index", "Home");

        // Parametreleri trim et
        subeAdi = subeAdi?.Trim();
        firma = firma?.Trim();
        grup = grup?.Trim();

        try
        {
            // Eğer forceRefresh geldi ise (Güncelle butonu basıldı), Session'ı güncelle
            if (forceRefresh == "true")
            {
                HttpContext.Session.SetString("LastGecmisSubeFilter", subeAdi ?? "");
                HttpContext.Session.SetString("LastGecmisFirmaFilter", firma ?? "");
                HttpContext.Session.SetString("LastGecmisGrupFilter", grup ?? "");
            }
            else if (string.IsNullOrEmpty(subeAdi) && string.IsNullOrEmpty(firma) && string.IsNullOrEmpty(grup))
            {
                // Eğer parametreler boş ise, Session'dan son filtreleri al
                subeAdi = HttpContext.Session.GetString("LastGecmisSubeFilter") ?? "";
                firma = HttpContext.Session.GetString("LastGecmisFirmaFilter") ?? "";
                grup = HttpContext.Session.GetString("LastGecmisGrupFilter") ?? "";
            }

            // Boş string'leri null'a dönüştür (SQL query için)
            var subeAdiForQuery = string.IsNullOrWhiteSpace(subeAdi) ? null : subeAdi;
            var firmaForQuery = string.IsNullOrWhiteSpace(firma) ? null : firma;
            var grupForQuery = string.IsNullOrWhiteSpace(grup) ? null : grup;

            var sevkiyatlar = new List<dynamic>();
            var subeler = new List<dynamic>();
            var firmalar = new List<dynamic>();
            var gruplar = new List<dynamic>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureSiparisLinkColumnAsync(conn);

                // Tüm bölümleri al
                var subeQuery = "SELECT DISTINCT Ad FROM stk_Sube WHERE AktifMi = true ORDER BY Ad";
                using (var subeCmd = new MySqlCommand(subeQuery, conn))
                using (var reader = await subeCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        subeler.Add(new { Ad = reader.GetString("Ad") });
                    }
                }

                // Tüm firmaları al
                var firmaQuery = "SELECT DISTINCT Firma FROM stk_Urun WHERE Firma IS NOT NULL AND Firma != '' ORDER BY Firma";
                using (var firmaCmd = new MySqlCommand(firmaQuery, conn))
                using (var reader = await firmaCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        firmalar.Add(new { Firma = reader.GetString("Firma") });
                    }
                }

                // Tüm grupları al
                var grupQuery = "SELECT DISTINCT Grup FROM stk_Urun WHERE Grup IS NOT NULL AND Grup != '' ORDER BY Grup";
                using (var grupCmd = new MySqlCommand(grupQuery, conn))
                using (var reader = await grupCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        gruplar.Add(new { Grup = reader.GetString("Grup") });
                    }
                }

                var query = @"
                    SELECT sv.*, e.Id as EksikId, u.Ad as UrunAdi, s.Ad as SubeAdi, fs.Id as FabrikaSiparisId,
                           CASE WHEN sv.Durum = 'Yolda' AND TIMESTAMPDIFF(HOUR, sv.SevkTarihi, NOW()) > 48 THEN 1 ELSE 0 END as GecikmeUyarisi,
                           u.Firma, u.Grup
                    FROM stk_Sevkiyat sv
                    JOIN stk_EksikKaydi e ON sv.EksikKaydiId = e.Id
                    JOIN stk_Urun u ON e.UrunId = u.Id
                    JOIN stk_Sube s ON e.SubeId = s.Id
                    LEFT JOIN stk_FabrikaSiparisi fs ON fs.KaynakSevkiyatId = sv.Id
                    WHERE sv.Durum <> 'Onaylandi'";

                // Bölüm filtrelemesi
                if (!string.IsNullOrWhiteSpace(subeAdiForQuery))
                {
                    query += " AND s.Ad = @SubeAdi";
                }

                // Firma filtrelemesi
                if (!string.IsNullOrWhiteSpace(firmaForQuery))
                {
                    query += " AND u.Firma = @Firma";
                }

                // Grup filtrelemesi
                if (!string.IsNullOrWhiteSpace(grupForQuery))
                {
                    query += " AND u.Grup = @Grup";
                }

                query += " ORDER BY sv.SevkTarihi DESC";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    if (!string.IsNullOrWhiteSpace(subeAdiForQuery))
                    {
                        cmd.Parameters.AddWithValue("@SubeAdi", subeAdiForQuery);
                    }
                    if (!string.IsNullOrWhiteSpace(firmaForQuery))
                    {
                        cmd.Parameters.AddWithValue("@Firma", firmaForQuery);
                    }
                    if (!string.IsNullOrWhiteSpace(grupForQuery))
                    {
                        cmd.Parameters.AddWithValue("@Grup", grupForQuery);
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            sevkiyatlar.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                EksikId = reader.GetInt32("EksikId"),
                                UrunAdi = reader.GetString("UrunAdi"),
                                SubeAdi = reader.GetString("SubeAdi"),
                                SevkMiktari = reader.GetDecimal("SevkMiktari"),
                                Durum = reader.GetString("Durum"),
                                SevkTarihi = Convert.ToDateTime(reader["SevkTarihi"]),
                                SubeOnayTarihi = reader["SubeOnayTarihi"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["SubeOnayTarihi"]),
                                FabrikayaAktarildi = reader["FabrikaSiparisId"] != DBNull.Value,
                                GecikmeUyarisi = reader["GecikmeUyarisi"] != DBNull.Value && Convert.ToInt32(reader["GecikmeUyarisi"]) == 1,
                                TeslimSuresiGun = reader["SubeOnayTarihi"] == DBNull.Value
                                    ? (double?)null
                                    : Math.Round((Convert.ToDateTime(reader["SubeOnayTarihi"]) - Convert.ToDateTime(reader["SevkTarihi"])).TotalDays, 1)
                            });
                        }
                    }
                }
            }

            ViewBag.Subeler = subeler;
            ViewBag.SeciiliSube = subeAdi;
            ViewBag.Firmalar = firmalar;
            ViewBag.SeciliFirma = firma;
            ViewBag.Gruplar = gruplar;
            ViewBag.SeciliGrup = grup;
            ViewBag.Rol = rol;
            ViewBag.IsAdmin = rol == "Admin";

            return View("~/Views/STS/Sevkiyat/SevkiyatGecmisi.cshtml", sevkiyatlar);
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // POST: Sevkiyat/Sevked - Eksik kaydını "SevkEdildi" olarak işaretle ve sevkiyat oluştur
    [HttpPost]
    public async Task<IActionResult> SevkEt(int eksikKaydiId)
    {
        try
        {
            var kullaniciId = HttpContext.Session.GetInt32("KullaniciId") ?? 0;

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureBildirimTableAsync(conn);

                // Eksik kaydının detaylarını al
                var queryExsik = "SELECT Miktar, SubeId FROM stk_EksikKaydi WHERE Id = @Id";
                decimal miktar = 0;
                int subeId = 0;
                using (var cmd = new MySqlCommand(queryExsik, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", eksikKaydiId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            miktar = Convert.ToDecimal(reader["Miktar"]);
                            subeId = Convert.ToInt32(reader["SubeId"]);
                        }
                    }
                }

                // Sevkiyat kaydı oluştur
                var insertQuery = @"
                    INSERT INTO stk_Sevkiyat (EksikKaydiId, SevkMiktari, SevkTarihi, SevkedenKullaniciId, Durum)
                    VALUES (@EksikKaydiId, @SevkMiktari, NOW(), @SevkedenKullaniciId, 'Yolda')";

                using (var cmd = new MySqlCommand(insertQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@EksikKaydiId", eksikKaydiId);
                    cmd.Parameters.AddWithValue("@SevkMiktari", miktar);
                    cmd.Parameters.AddWithValue("@SevkedenKullaniciId", kullaniciId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Eksik kaydının durumunu güncelle
                var updateQuery = "UPDATE stk_EksikKaydi SET Durum = 'SevkEdildi' WHERE Id = @Id";
                using (var cmd = new MySqlCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", eksikKaydiId);
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var bildirimCmd = new MySqlCommand(@"INSERT INTO stk_Bildirim (HedefRol, HedefSubeId, Baslik, Mesaj, Link, OkunduMu)
                                                            VALUES ('SubePersoneli', @HedefSubeId, @Baslik, @Mesaj, @Link, false)", conn))
                {
                    bildirimCmd.Parameters.AddWithValue("@HedefSubeId", subeId);
                    bildirimCmd.Parameters.AddWithValue("@Baslik", "Sevkiyat Yapıldı");
                    bildirimCmd.Parameters.AddWithValue("@Mesaj", "Ana depo eksik kaydınız için sevkiyat çıkışı yaptı.");
                    bildirimCmd.Parameters.AddWithValue("@Link", "/Sevkiyat/SubeOnay");
                    await bildirimCmd.ExecuteNonQueryAsync();
                }
            }

            // Son filtreleri Session'dan al ve aynı sayfada kal
            var lastSube = HttpContext.Session.GetString("LastSubeFilter") ?? "";
            var lastFirma = HttpContext.Session.GetString("LastFirmaFilter") ?? "";
            var lastGrup = HttpContext.Session.GetString("LastGrupFilter") ?? "";

            return RedirectToAction("SevkBekleyenEksikler", new { subeAdi = lastSube, firma = lastFirma, grup = lastGrup, forceRefresh = "true" });
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // GET: Sevkiyat/SubeOnay - Şubeler gelen ürünleri onaylama (DepoSorumlusu tüm şubeleri görebilir)
    public async Task<IActionResult> SubeOnay()
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        var subeId = HttpContext.Session.GetInt32("SubeId") ?? 0;
        
        // SubePersoneli veya DepoSorumlusu rolündeyse erişime izin ver
        if (rol != "SubePersoneli" && rol != "DepoSorumlusu") 
            return RedirectToAction("Login", "Account");

        try
        {
            var sevkiyatlar = new List<dynamic>();
            var depoSiparisleri = new List<dynamic>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                var query = @"
                    SELECT sv.*, e.Id as EksikId, u.Ad as UrunAdi, s.Ad as SubeAdi
                    FROM stk_Sevkiyat sv
                    JOIN stk_EksikKaydi e ON sv.EksikKaydiId = e.Id
                    JOIN stk_Urun u ON e.UrunId = u.Id
                    JOIN stk_Sube s ON e.SubeId = s.Id
                    WHERE sv.Durum IN ('Yolda', 'OnayBekliyor') AND e.SubeId = @SubeId";

                query += " ORDER BY sv.SevkTarihi DESC";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@SubeId", subeId);
                    
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            sevkiyatlar.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                UrunAdi = reader.GetString("UrunAdi"),
                                SubeAdi = reader.GetString("SubeAdi"),
                                SevkMiktari = reader.GetDecimal("SevkMiktari"),
                                Durum = reader.GetString("Durum"),
                                SevkTarihi = Convert.ToDateTime(reader["SevkTarihi"])
                            });
                        }
                    }
                }

                // DepoSorumlusu ise OnayBekliyor durumundaki siparişleri de getir
                if (rol == "DepoSorumlusu")
                {
                    var depoQuery = @"
                        SELECT sv.*, e.Id as EksikId, u.Ad as UrunAdi, s.Ad as SubeAdi
                        FROM stk_Sevkiyat sv
                        JOIN stk_EksikKaydi e ON sv.EksikKaydiId = e.Id
                        JOIN stk_Urun u ON e.UrunId = u.Id
                        JOIN stk_Sube s ON e.SubeId = s.Id
                        WHERE sv.Durum = 'OnayBekliyor' AND e.SubeId = @DepoSubeId
                        ORDER BY sv.SevkTarihi DESC";

                    using (var depoCmd = new MySqlCommand(depoQuery, conn))
                    {
                        depoCmd.Parameters.AddWithValue("@DepoSubeId", subeId);
                        
                        using (var reader = await depoCmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                depoSiparisleri.Add(new
                                {
                                    Id = reader.GetInt32("Id"),
                                    UrunAdi = reader.GetString("UrunAdi"),
                                    SubeAdi = reader.GetString("SubeAdi"),
                                    SevkMiktari = reader.GetDecimal("SevkMiktari"),
                                    Durum = reader.GetString("Durum"),
                                    SevkTarihi = Convert.ToDateTime(reader["SevkTarihi"])
                                });
                            }
                        }
                    }
                }
            }

            ViewBag.Rol = rol;
            ViewBag.IsDepoSorumlusu = rol == "DepoSorumlusu";
            ViewBag.DepoSiparisleri = depoSiparisleri;

            return View("~/Views/STS/Sevkiyat/SubeOnay.cshtml", sevkiyatlar);
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // POST: Sevkiyat/OnayEt - Şube ürünü onaylama (DepoSorumlusu da onaylayabilir)
    [HttpPost]
    public async Task<IActionResult> OnayEt(int sevkiyatId, string notu = "")
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        var subeId = HttpContext.Session.GetInt32("SubeId") ?? 0;
        
        // Sadece DepoSorumlusu veya ilgili şubenin personeli onay yapabilir
        if (rol != "DepoSorumlusu" && rol != "SubePersoneli")
        {
            return RedirectToAction("Index", "Home");
        }

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                // Sevkiyatın detaylarını al - DepoSorumlusu değilse şube kontrolü yap
                var detailQuery = @"
                    SELECT sv.Id, sv.EksikKaydiId, e.SubeId 
                    FROM stk_Sevkiyat sv
                    JOIN stk_EksikKaydi e ON sv.EksikKaydiId = e.Id
                    WHERE sv.Id = @Id";

                int eksikKaydiId = 0;
                int sevkiyatSubeId = 0;

                using (var detailCmd = new MySqlCommand(detailQuery, conn))
                {
                    detailCmd.Parameters.AddWithValue("@Id", sevkiyatId);
                    using (var reader = await detailCmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            return BadRequest("Sevkiyat bulunamadı");
                        }
                        eksikKaydiId = reader.GetInt32("EksikKaydiId");
                        sevkiyatSubeId = reader.GetInt32("SubeId");
                    }
                }

                // Yetki kontrolü - DepoSorumlusu tüm şubeleri onaylayabilir
                if (rol != "DepoSorumlusu" && sevkiyatSubeId != subeId)
                {
                    return RedirectToAction("Index", "Home");
                }

                var updateQuery = @"
                    UPDATE stk_Sevkiyat 
                    SET Durum = 'Onaylandi', SubeOnayTarihi = NOW(), SubeOnayNotu = @Notu
                    WHERE Id = @Id";

                using (var cmd = new MySqlCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", sevkiyatId);
                    cmd.Parameters.AddWithValue("@Notu", notu);
                    await cmd.ExecuteNonQueryAsync();
                }

                if (eksikKaydiId > 0)
                {
                    using (var eksikCmd = new MySqlCommand("UPDATE stk_EksikKaydi SET Durum = 'Tamamlandi' WHERE Id = @Id", conn))
                    {
                        eksikCmd.Parameters.AddWithValue("@Id", eksikKaydiId);
                        await eksikCmd.ExecuteNonQueryAsync();
                    }
                }
            }

            return RedirectToAction("SubeOnay");
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Reddet(int sevkiyatId, string neden = "")
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        var subeId = HttpContext.Session.GetInt32("SubeId") ?? 0;
        
        // Sadece DepoSorumlusu veya ilgili şubenin personeli reddet yapabilir
        if (rol != "DepoSorumlusu" && rol != "SubePersoneli")
        {
            return RedirectToAction("Index", "Home");
        }

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureBildirimTableAsync(conn);

                // Sevkiyatın detaylarını al - DepoSorumlusu değilse şube kontrolü yap
                var detailQuery = @"
                    SELECT sv.Id, sv.EksikKaydiId, e.SubeId 
                    FROM stk_Sevkiyat sv
                    JOIN stk_EksikKaydi e ON sv.EksikKaydiId = e.Id
                    WHERE sv.Id = @Id";

                int eksikKaydiId = 0;
                int sevkiyatSubeId = 0;

                using (var detailCmd = new MySqlCommand(detailQuery, conn))
                {
                    detailCmd.Parameters.AddWithValue("@Id", sevkiyatId);
                    using (var reader = await detailCmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            return BadRequest("Sevkiyat bulunamadı");
                        }
                        eksikKaydiId = reader.GetInt32("EksikKaydiId");
                        sevkiyatSubeId = reader.GetInt32("SubeId");
                    }
                }

                // Yetki kontrolü - DepoSorumlusu tüm şubeleri reddedebilir
                if (rol != "DepoSorumlusu" && sevkiyatSubeId != subeId)
                {
                    return RedirectToAction("Index", "Home");
                }

                using (var cmd = new MySqlCommand(@"
                    UPDATE stk_Sevkiyat
                    SET Durum = 'IadeEdildi', SubeOnayTarihi = NOW(), SubeOnayNotu = @Notu
                    WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", sevkiyatId);
                    cmd.Parameters.AddWithValue("@Notu", string.IsNullOrWhiteSpace(neden) ? "Şube iade etti" : $"İade Nedeni: {neden}");
                    await cmd.ExecuteNonQueryAsync();
                }

                if (eksikKaydiId > 0)
                {
                    using (var eksikCmd = new MySqlCommand("UPDATE stk_EksikKaydi SET Durum = 'Bekliyor' WHERE Id = @Id", conn))
                    {
                        eksikCmd.Parameters.AddWithValue("@Id", eksikKaydiId);
                        await eksikCmd.ExecuteNonQueryAsync();
                    }
                }

                using (var bildirimCmd = new MySqlCommand(@"INSERT INTO stk_Bildirim (HedefRol, HedefSubeId, Baslik, Mesaj, Link, OkunduMu)
                                                            VALUES ('DepoSorumlusu', NULL, @Baslik, @Mesaj, @Link, false)", conn))
                {
                    bildirimCmd.Parameters.AddWithValue("@Baslik", "Sevkiyat İade Edildi");
                    bildirimCmd.Parameters.AddWithValue("@Mesaj", $"Sevkiyat #{sevkiyatId} şube tarafından iade edildi. Neden: {(string.IsNullOrWhiteSpace(neden) ? "Belirtilmedi" : neden)}");
                    bildirimCmd.Parameters.AddWithValue("@Link", "/Sevkiyat/Index");
                    await bildirimCmd.ExecuteNonQueryAsync();
                }

                using (var bildirimCmd = new MySqlCommand(@"INSERT INTO stk_Bildirim (HedefRol, HedefSubeId, Baslik, Mesaj, Link, OkunduMu)
                                                            VALUES ('Admin', NULL, @Baslik, @Mesaj, @Link, false)", conn))
                {
                    bildirimCmd.Parameters.AddWithValue("@Baslik", "Sevkiyat İade Edildi");
                    bildirimCmd.Parameters.AddWithValue("@Mesaj", $"Sevkiyat #{sevkiyatId} şube tarafından iade edildi. Neden: {(string.IsNullOrWhiteSpace(neden) ? "Belirtilmedi" : neden)}");
                    bildirimCmd.Parameters.AddWithValue("@Link", "/Sevkiyat/Index");
                    await bildirimCmd.ExecuteNonQueryAsync();
                }
            }

            TempData["Basari"] = "Sevkiyat iade olarak işaretlendi ve depo bilgilendirildi.";
            return RedirectToAction("SubeOnay");
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // POST: Sevkiyat/SilEksik - Sevk bekleyen eksik kaydını sil (Admin)
    [HttpPost]
    public async Task<IActionResult> SilEksik(int eksikKaydiId, string silmeSebebi = "")
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        var kullaniciId = HttpContext.Session.GetInt32("KullaniciId") ?? 0;
        string urunAdi = "";

        // Sadece Admin silebilir
        if (rol != "Admin")
        {
            return RedirectToAction("Index", "Home");
        }

        if (string.IsNullOrWhiteSpace(silmeSebebi))
        {
            TempData["Hata"] = "Silme sebebi boş bırakılamaz.";
            return RedirectToAction("Index");
        }

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                // Eksik kaydının detaylarını al
                var detailQuery = @"
                    SELECT u.Ad as UrunAdi, s.Ad as SubeAdi
                    FROM stk_EksikKaydi e
                    JOIN stk_Urun u ON e.UrunId = u.Id
                    JOIN stk_Sube s ON e.SubeId = s.Id
                    WHERE e.Id = @Id";

                var subeAdi = "";

                using (var cmd = new MySqlCommand(detailQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", eksikKaydiId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            TempData["Hata"] = "Eksik kaydı bulunamadı.";
                            return RedirectToAction("Index");
                        }
                        urunAdi = reader.GetString("UrunAdi");
                        subeAdi = reader.GetString("SubeAdi");
                    }
                }

                // Eksik kaydını "Silindi" olarak işaretle
                var updateQuery = @"
                    UPDATE stk_EksikKaydi 
                    SET Durum = 'Silindi', SilmeSebebi = @SilmeSebebi, SilmeTarihi = NOW(), SilenKullaniciId = @SilenKullaniciId
                    WHERE Id = @Id";

                using (var cmd = new MySqlCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", eksikKaydiId);
                    cmd.Parameters.AddWithValue("@SilmeSebebi", silmeSebebi);
                    cmd.Parameters.AddWithValue("@SilenKullaniciId", kullaniciId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // İlgili sevkiyatları "Silindi" olarak işaretle
                var sevkiyatUpdateQuery = @"
                    UPDATE stk_Sevkiyat 
                    SET Durum = 'Silindi'
                    WHERE EksikKaydiId = @EksikKaydiId";

                using (var cmd = new MySqlCommand(sevkiyatUpdateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@EksikKaydiId", eksikKaydiId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // HareketLog'a kayıt ekle
                var logQuery = @"
                    INSERT INTO stk_HareketLog (TabloAdi, KayitId, IslemTipi, YapanKullaniciId, IslemTarihi, EskiDeger, YeniDeger)
                    VALUES ('stk_EksikKaydi', @KayitId, 'Sil', @KullaniciId, NOW(), 
                            CONCAT('Ürün: ', @UrunAdi, ' | Şube: ', @SubeAdi), 
                            CONCAT('Silme Sebebi: ', @SilmeSebebi))";

                using (var cmd = new MySqlCommand(logQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@KayitId", eksikKaydiId);
                    cmd.Parameters.AddWithValue("@KullaniciId", kullaniciId);
                    cmd.Parameters.AddWithValue("@UrunAdi", urunAdi);
                    cmd.Parameters.AddWithValue("@SubeAdi", subeAdi);
                    cmd.Parameters.AddWithValue("@SilmeSebebi", silmeSebebi);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            TempData["Basari"] = $"Eksik kaydı '{urunAdi}' başarıyla silindi.";
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Silme işlemi başarısız: {ex.Message}";
            return RedirectToAction("Index");
        }
    }

    private async Task EnsureEksikKaydiSilColumnAsync(MySqlConnection conn)
    {
        // Alter Durum enum
        try
        {
            var checkDurumQuery = @"
                SELECT COLUMN_TYPE FROM information_schema.COLUMNS 
                WHERE TABLE_SCHEMA = DATABASE() 
                AND TABLE_NAME = 'stk_EksikKaydi' 
                AND COLUMN_NAME = 'Durum'";

            using (var cmd = new MySqlCommand(checkDurumQuery, conn))
            {
                var result = await cmd.ExecuteScalarAsync();
                if (result != null && !result.ToString().Contains("Silindi"))
                {
                    using (var alterCmd = new MySqlCommand(
                        "ALTER TABLE stk_EksikKaydi MODIFY Durum ENUM('Bekliyor', 'SevkEdildi', 'Tamamlandi', 'Silindi') NOT NULL DEFAULT 'Bekliyor'",
                        conn))
                    {
                        await alterCmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }
        catch { }

        // Silme sütunları ekle
        var columns = new[] { "SilindiMi", "SilmeSebebi", "SilmeTarihi", "SilenKullaniciId" };
        foreach (var col in columns)
        {
            try
            {
                var checkQuery = @"
                    SELECT COUNT(*) FROM information_schema.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                    AND TABLE_NAME = 'stk_EksikKaydi' 
                    AND COLUMN_NAME = @ColName";

                using (var cmd = new MySqlCommand(checkQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@ColName", col);
                    var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0) > 0;
                    if (!exists)
                    {
                        string alterSql = col switch
                        {
                            "SilindiMi" => "ALTER TABLE stk_EksikKaydi ADD COLUMN SilindiMi BOOLEAN DEFAULT FALSE AFTER Durum",
                            "SilmeSebebi" => "ALTER TABLE stk_EksikKaydi ADD COLUMN SilmeSebebi VARCHAR(500) NULL AFTER SilindiMi",
                            "SilmeTarihi" => "ALTER TABLE stk_EksikKaydi ADD COLUMN SilmeTarihi DATETIME NULL AFTER SilmeSebebi",
                            "SilenKullaniciId" => "ALTER TABLE stk_EksikKaydi ADD COLUMN SilenKullaniciId INT NULL AFTER SilmeTarihi",
                            _ => null
                        };

                        if (!string.IsNullOrEmpty(alterSql))
                        {
                            using (var alterCmd = new MySqlCommand(alterSql, conn))
                            {
                                await alterCmd.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        // stk_Sevkiyat Durum enum'a Silindi ekle
        try
        {
            var checkDurumQuery = @"
                SELECT COLUMN_TYPE FROM information_schema.COLUMNS 
                WHERE TABLE_SCHEMA = DATABASE() 
                AND TABLE_NAME = 'stk_Sevkiyat' 
                AND COLUMN_NAME = 'Durum'";

            using (var cmd = new MySqlCommand(checkDurumQuery, conn))
            {
                var result = await cmd.ExecuteScalarAsync();
                if (result != null && !result.ToString().Contains("Silindi"))
                {
                    using (var alterCmd = new MySqlCommand(
                        "ALTER TABLE stk_Sevkiyat MODIFY Durum ENUM('Hazirlaniyor', 'Yolda', 'TeslimEdildi', 'OnayBekliyor', 'Onaylandi', 'IadeEdildi', 'Silindi') NOT NULL DEFAULT 'Hazirlaniyor'",
                        conn))
                    {
                        await alterCmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }
        catch { }
    }

    private async Task EnsureSiparisLinkColumnAsync(MySqlConnection conn)
    {
        var tableQuery = @"
            CREATE TABLE IF NOT EXISTS stk_FabrikaSiparisi (
                Id INT PRIMARY KEY AUTO_INCREMENT,
                KaynakSevkiyatId INT NULL,
                UrunId INT NOT NULL,
                Miktar DECIMAL(10,2) NOT NULL,
                HaftaNo VARCHAR(20) NOT NULL,
                SiparisTarihi DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                Durum VARCHAR(50) NOT NULL DEFAULT 'SiparisVerildi',
                TahminiTeslimTarihi DATETIME NULL,
                `Not` VARCHAR(500) NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;";

        using (var tableCmd = new MySqlCommand(tableQuery, conn))
        {
            await tableCmd.ExecuteNonQueryAsync();
        }

        var columnQuery = @"
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'stk_FabrikaSiparisi'
              AND COLUMN_NAME = 'KaynakSevkiyatId';";

        using (var cmd = new MySqlCommand(columnQuery, conn))
        {
            var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0) > 0;
            if (!exists)
            {
                using (var alterCmd = new MySqlCommand("ALTER TABLE stk_FabrikaSiparisi ADD COLUMN KaynakSevkiyatId INT NULL AFTER Id;", conn))
                {
                    await alterCmd.ExecuteNonQueryAsync();
                }
            }
        }
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

    // GET: Sevkiyat/ExportBekleyenEksikler - Sevk bekleyen eksikleri Excel'e aktar
    public async Task<IActionResult> ExportBekleyenEksikler(string? subeAdi = null, string? firma = null, string? grup = null)
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        if (rol != "DepoSorumlusu" && rol != "Admin")
            return RedirectToAction("Index", "Home");

        try
        {
            var bekleyenEksikler = new List<dynamic>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                var bekleyenQuery = @"
                    SELECT e.Id, e.SiparisNo, u.Ad as UrunAdi, s.Ad as SubeAdi, u.Firma, u.Grup, e.Miktar, e.AcilMi, e.GeciktiMi, e.GirisTarihi
                    FROM stk_EksikKaydi e
                    JOIN stk_Urun u ON e.UrunId = u.Id
                    JOIN stk_Sube s ON e.SubeId = s.Id
                    WHERE e.Durum = 'Bekliyor'";

                if (!string.IsNullOrWhiteSpace(subeAdi))
                {
                    bekleyenQuery += " AND s.Ad = @SubeAdi";
                }

                if (!string.IsNullOrWhiteSpace(firma))
                {
                    bekleyenQuery += " AND u.Firma = @Firma";
                }

                if (!string.IsNullOrWhiteSpace(grup))
                {
                    bekleyenQuery += " AND u.Grup = @Grup";
                }

                bekleyenQuery += " ORDER BY e.GeciktiMi DESC, e.AcilMi DESC, e.GirisTarihi ASC";

                using (var cmd = new MySqlCommand(bekleyenQuery, conn))
                {
                    if (!string.IsNullOrWhiteSpace(subeAdi))
                    {
                        cmd.Parameters.AddWithValue("@SubeAdi", subeAdi);
                    }
                    if (!string.IsNullOrWhiteSpace(firma))
                    {
                        cmd.Parameters.AddWithValue("@Firma", firma);
                    }
                    if (!string.IsNullOrWhiteSpace(grup))
                    {
                        cmd.Parameters.AddWithValue("@Grup", grup);
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            bekleyenEksikler.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                SiparisNo = reader["SiparisNo"] == DBNull.Value ? string.Empty : reader["SiparisNo"]?.ToString() ?? string.Empty,
                                UrunAdi = reader.GetString("UrunAdi"),
                                SubeAdi = reader.GetString("SubeAdi"),
                                Firma = reader["Firma"] == DBNull.Value ? string.Empty : reader["Firma"]?.ToString() ?? string.Empty,
                                Grup = reader["Grup"] == DBNull.Value ? string.Empty : reader["Grup"]?.ToString() ?? string.Empty,
                                Miktar = reader.GetDecimal("Miktar"),
                                AcilMi = reader["AcilMi"] != DBNull.Value && Convert.ToBoolean(reader["AcilMi"]),
                                GeciktiMi = reader["GeciktiMi"] != DBNull.Value && Convert.ToBoolean(reader["GeciktiMi"]),
                                GirisTarihi = Convert.ToDateTime(reader["GirisTarihi"])
                            });
                        }
                    }
                }
            }

            // Excel dosyası oluştur
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
            using (var package = new OfficeOpenXml.ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Sevk Bekleyen Eksikler");

                // Başlıkları ekle
                worksheet.Cells[1, 1].Value = "Sipariş No";
                worksheet.Cells[1, 2].Value = "Ürün Adı";
                worksheet.Cells[1, 3].Value = "Miktar";
                worksheet.Cells[1, 4].Value = "Şube";
                worksheet.Cells[1, 5].Value = "Firma";
                worksheet.Cells[1, 6].Value = "Grup";
                worksheet.Cells[1, 7].Value = "Acil";
                worksheet.Cells[1, 8].Value = "Gecikti";
                worksheet.Cells[1, 9].Value = "Giriş Tarihi";

                // Başlık stilini ayarla
                using (var headerRange = worksheet.Cells[1, 1, 1, 9])
                {
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    headerRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

                // Verileri ekle
                int row = 2;
                foreach (var eksik in bekleyenEksikler)
                {
                    worksheet.Cells[row, 1].Value = eksik.SiparisNo;
                    worksheet.Cells[row, 2].Value = eksik.UrunAdi;
                    worksheet.Cells[row, 3].Value = eksik.Miktar;
                    worksheet.Cells[row, 4].Value = eksik.SubeAdi;
                    worksheet.Cells[row, 5].Value = eksik.Firma;
                    worksheet.Cells[row, 6].Value = eksik.Grup;
                    worksheet.Cells[row, 7].Value = eksik.AcilMi ? "Evet" : "Hayır";
                    worksheet.Cells[row, 8].Value = eksik.GeciktiMi ? "Evet" : "Hayır";
                    worksheet.Cells[row, 9].Value = eksik.GirisTarihi;
                    worksheet.Cells[row, 9].Style.Numberformat.Format = "dd.mm.yyyy hh:mm";

                    row++;
                }

                // Sütun genişliklerini otomatik ayarla
                worksheet.Cells[1, 1, bekleyenEksikler.Count + 1, 9].AutoFitColumns();

                // Excel dosyasını byte array olarak al
                var excelBytes = package.GetAsByteArray();

                // Dosya adı oluştur
                var fileName = $"SevkBekleyenEksikler_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                // Excel dosyasını indir
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Excel dosyası oluşturulurken hata oluştu: {ex.Message}";
            return RedirectToAction("Index");
        }
    }
}
