using Microsoft.AspNetCore.Mvc;
using gamabelmvc.Services;
using MySqlConnector;

namespace gamabelmvc.Controllers.STS;

public class RaporStsController : Controller
{
    private readonly DbConnectionFactory _dbFactory;

    public RaporStsController(DbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // GET: Rapor/Index - Eksik trendi, geç giren şubeler, en çok eksik ürün
    public async Task<IActionResult> Index()
    {
        // Rol kontrolü: Sadece DepoSorumlusu ve Admin erişebilir
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        if (rol != "DepoSorumlusu" && rol != "Admin")
            return RedirectToAction("Index", "Home");

        try
        {
            var raporData = new Dictionary<string, object>();
            var haftaNo = GetHaftaNo();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                // 1. En çok eksik ürün
                var enCokEksikUrunler = new List<dynamic>();
                using (var cmd = new MySqlCommand(@"
                    SELECT u.Ad, u.Birim, COUNT(*) as Adet
                    FROM stk_EksikKaydi e
                    JOIN stk_Urun u ON e.UrunId = u.Id
                    GROUP BY u.Id, u.Ad, u.Birim
                    ORDER BY Adet DESC
                    LIMIT 10", conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            enCokEksikUrunler.Add(new
                            {
                                UrunAdi = reader.GetString("Ad"),
                                Birim = reader["Birim"] == DBNull.Value ? string.Empty : reader["Birim"]?.ToString() ?? string.Empty,
                                Adet = reader.GetInt32("Adet")
                            });
                        }
                    }
                }
                raporData["EnCokEksikUrunler"] = enCokEksikUrunler;

                // 2. Geç giren şubeler (Pazartesi kapanışından sonra giren)
                var gecGirenSubeler = new List<dynamic>();
                using (var cmd = new MySqlCommand(@"
                    SELECT s.Ad, COUNT(*) as Adet, MAX(e.GirisTarihi) as SonGirisTarihi
                    FROM stk_EksikKaydi e
                    JOIN stk_Sube s ON e.SubeId = s.Id
                    GROUP BY s.Id
                    ORDER BY Adet DESC", conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            gecGirenSubeler.Add(new
                            {
                                SubeAdi = reader.GetString("Ad"),
                                EksikSayisi = reader.GetInt32("Adet"),
                                SonGirisTarihi = Convert.ToDateTime(reader["SonGirisTarihi"])
                            });
                        }
                    }
                }
                raporData["GecGirenSubeler"] = gecGirenSubeler;

                // 3. Eksik trendi (Haftalık)
                var eksikTrendi = new List<dynamic>();
                using (var cmd = new MySqlCommand(@"
                    SELECT HaftaNo, COUNT(*) as ToplamEksik
                    FROM stk_EksikKaydi
                    GROUP BY HaftaNo
                    ORDER BY HaftaNo DESC
                    LIMIT 13", conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            eksikTrendi.Add(new
                            {
                                HaftaNo = reader.GetString("HaftaNo"),
                                ToplamEksik = reader.GetInt32("ToplamEksik")
                            });
                        }
                    }
                }
                raporData["EksikTrendi"] = eksikTrendi;

                // 4. Acil eksiklerin durumu
                var acilEksikler = new List<dynamic>();
                using (var cmd = new MySqlCommand(@"
                    SELECT s.Ad, u.Ad as UrunAdi, u.Birim, e.Miktar, e.Durum
                    FROM stk_EksikKaydi e
                    JOIN stk_Sube s ON e.SubeId = s.Id
                    JOIN stk_Urun u ON e.UrunId = u.Id
                    WHERE e.AcilMi = true
                    ORDER BY e.GirisTarihi DESC", conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            acilEksikler.Add(new
                            {
                                SubeAdi = reader.GetString("Ad"),
                                UrunAdi = reader.GetString("UrunAdi"),
                                Birim = reader["Birim"] == DBNull.Value ? string.Empty : reader["Birim"]?.ToString() ?? string.Empty,
                                Miktar = reader.GetDecimal("Miktar"),
                                Durum = reader.GetString("Durum")
                            });
                        }
                    }
                }
                raporData["AcilEksikler"] = acilEksikler;
            }

            ViewBag.RaporData = raporData;
            ViewBag.HaftaNo = haftaNo;
            return View("~/Views/STS/RaporSts/Index.cshtml", raporData);
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // GET: Rapor/GeçmisHareketler - Tüm kayıtların logu (düzeltmeler + kim yaptı)
    public async Task<IActionResult> GeçmisHareketler()
    {
        // Rol kontrolü: Sadece DepoSorumlusu ve Admin erişebilir
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        if (rol != "DepoSorumlusu" && rol != "Admin")
            return RedirectToAction("Index", "Home");

        try
        {
            var hareketler = new List<dynamic>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                var query = @"
                    SELECT hl.*, k.AdSoyad
                    FROM stk_HareketLog hl
                    JOIN stk_Kullanici k ON hl.YapanKullaniciId = k.Id
                    ORDER BY hl.IslemTarihi DESC
                    LIMIT 1000";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            hareketler.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                TabloAdi = reader.GetString("TabloAdi"),
                                KayitId = reader.GetInt32("KayitId"),
                                IslemTipi = reader.GetString("IslemTipi"),
                                YapanKullanici = reader.GetString("AdSoyad"),
                                IslemTarihi = Convert.ToDateTime(reader["IslemTarihi"]),
                                EskiDeger = reader["EskiDeger"] == DBNull.Value ? null : reader["EskiDeger"]?.ToString(),
                                YeniDeger = reader["YeniDeger"] == DBNull.Value ? null : reader["YeniDeger"]?.ToString()
                            });
                        }
                    }
                }
            }

            return View("~/Views/STS/RaporSts/GeçmisHareketler.cshtml", hareketler);
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // GET: RaporSts/DetailedRapor - Ayrıntılı Rapor (Büfe, Firma, Tarih Filtresi)
    public async Task<IActionResult> DetailedRapor(int? subeId, string? firma, DateTime? baslangicTarihi, DateTime? bitisTarihi)
    {
        // Rol kontrolü: Sadece DepoSorumlusu ve Admin erişebilir
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        if (rol != "DepoSorumlusu" && rol != "Admin")
            return RedirectToAction("Index", "Home");

        try
        {
            var subeleri = new List<dynamic>();
            var firmalar = new List<string>();
            var islemler = new List<dynamic>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                // Şubeleri getir
                using (var cmd = new MySqlCommand("SELECT Id, Ad FROM stk_Sube ORDER BY Ad", conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            subeleri.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                Ad = reader.GetString("Ad")
                            });
                        }
                    }
                }

                // Firmaları getir (benzersiz)
                using (var cmd = new MySqlCommand("SELECT DISTINCT Firma FROM stk_Urun WHERE Firma IS NOT NULL ORDER BY Firma", conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            firmalar.Add(reader.GetString("Firma"));
                        }
                    }
                }

                // Ayrıntılı işlemleri getir (filter uygulanmışsa)
                if (subeId.HasValue && baslangicTarihi.HasValue && bitisTarihi.HasValue)
                {
                    var query = @"
                        SELECT 
                            e.Id as EksikId,
                            e.GirisTarihi,
                            e.HaftaNo,
                            s.Ad as SubeAdi,
                            u.Ad as UrunAdi,
                            u.Firma,
                            e.Miktar as EksikMiktar,
                            e.Durum as EksikDurum,
                            e.AcilMi,
                            '' as EksikNotu,
                            sv.Id as SevkiyatId,
                            sv.SevkTarihi,
                            sv.SevkMiktari,
                            sv.Durum as SevkiyatDurum,
                            sv.SubeOnayTarihi,
                            k.AdSoyad as SevkedenKullanici
                        FROM stk_EksikKaydi e
                        JOIN stk_Sube s ON e.SubeId = s.Id
                        JOIN stk_Urun u ON e.UrunId = u.Id
                        LEFT JOIN stk_Sevkiyat sv ON e.Id = sv.EksikKaydiId
                        LEFT JOIN stk_Kullanici k ON sv.SevkedenKullaniciId = k.Id
                        WHERE s.Id = @SubeId
                            AND e.GirisTarihi >= @BaslangicTarihi
                            AND e.GirisTarihi <= @BitisTarihi
                            AND (@Firma = '' OR u.Firma = @Firma)
                        ORDER BY e.GirisTarihi DESC, sv.SevkTarihi DESC";

                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@SubeId", subeId);
                        cmd.Parameters.AddWithValue("@BaslangicTarihi", baslangicTarihi.Value.Date);
                        cmd.Parameters.AddWithValue("@BitisTarihi", bitisTarihi.Value.Date.AddHours(23).AddMinutes(59).AddSeconds(59));
                        cmd.Parameters.AddWithValue("@Firma", firma ?? "");

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                islemler.Add(new
                                {
                                    EksikId = reader.GetInt32("EksikId"),
                                    GirisTarihi = Convert.ToDateTime(reader["GirisTarihi"]),
                                    HaftaNo = reader.GetString("HaftaNo"),
                                    SubeAdi = reader.GetString("SubeAdi"),
                                    UrunAdi = reader.GetString("UrunAdi"),
                                    Firma = reader["Firma"] == DBNull.Value ? "" : reader["Firma"]?.ToString() ?? "",
                                    EksikMiktar = reader.GetDecimal("EksikMiktar"),
                                    EksikDurum = reader.GetString("EksikDurum"),
                                    AcilMi = reader.GetBoolean("AcilMi"),
                                    EksikNotu = reader["EksikNotu"] == DBNull.Value ? "" : reader["EksikNotu"]?.ToString() ?? "",
                                    SevkiyatId = reader["SevkiyatId"] == DBNull.Value ? 0 : reader.GetInt32("SevkiyatId"),
                                    SevkTarihi = reader["SevkTarihi"] == DBNull.Value ? (DateTime?)null : (DateTime?)Convert.ToDateTime(reader["SevkTarihi"]),
                                    SevkMiktari = reader["SevkMiktari"] == DBNull.Value ? 0m : reader.GetDecimal("SevkMiktari"),
                                    SevkiyatDurum = reader["SevkiyatDurum"] == DBNull.Value ? "" : reader["SevkiyatDurum"]?.ToString() ?? "",
                                    SubeOnayTarihi = reader["SubeOnayTarihi"] == DBNull.Value ? (DateTime?)null : (DateTime?)Convert.ToDateTime(reader["SubeOnayTarihi"]),
                                    SevkedenKullanici = reader["SevkedenKullanici"] == DBNull.Value ? "" : reader["SevkedenKullanici"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
            }

            ViewBag.Subeleri = subeleri;
            ViewBag.Firmalar = firmalar;
            ViewBag.Islemler = islemler;
            ViewBag.SeciliSubeId = subeId;
            ViewBag.SeciliFirma = firma;
            ViewBag.BaslangicTarihi = baslangicTarihi?.ToString("yyyy-MM-dd");
            ViewBag.BitisTarihi = bitisTarihi?.ToString("yyyy-MM-dd");
            ViewBag.FiltreLendiMi = subeId.HasValue && baslangicTarihi.HasValue && bitisTarihi.HasValue;

            return View("~/Views/STS/RaporSts/DetailedRapor.cshtml");
        }
        catch (Exception ex)
        {
            ViewBag.Hata = $"Hata: {ex.Message}";
            return View("~/Views/STS/RaporSts/DetailedRapor.cshtml");
        }
    }

    private string GetHaftaNo()
    {
        var today = DateTime.Now;
        var cultureInfo = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var weekOfYear = cultureInfo.Calendar.GetWeekOfYear(today, System.Globalization.CalendarWeekRule.FirstFullWeek, DayOfWeek.Monday);
        return $"{today.Year}-W{weekOfYear:D2}";
    }
}
