using Microsoft.AspNetCore.Mvc;
using gamabelmvc.Services;
using MySqlConnector;
using System.Globalization;

namespace gamabelmvc.Controllers.PRS;

public class HizliMesaiGirisiController : Controller
{
    private readonly DbConnectionFactory _dbFactory;
    private readonly ILogger<HizliMesaiGirisiController> _logger;

    public HizliMesaiGirisiController(DbConnectionFactory dbFactory, ILogger<HizliMesaiGirisiController> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    // GET: HizliMesaiGirisi/Index - Ana takvim sayfası (Tablo görünümü)
    public async Task<IActionResult> Index(int? yil = null, int? ay = null, string? birim = null)
    {
        // PersonelId (Personel Portalı) veya KullaniciId (STS) kontrol et
        var kullaniciId = HttpContext.Session.GetInt32("PersonelId") ?? HttpContext.Session.GetInt32("KullaniciId");
        
        // Test amaçlı: Oturum yoksa varsayılan kullanıcı ID'si kullan (1)
        kullaniciId = kullaniciId ?? 1;
        var rol = HttpContext.Session.GetString("Rol") ?? "birim_amiri";
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";

        try
        {
            // Ay ve yıl varsayılan değerler
            yil = yil ?? DateTime.Now.Year;
            ay = ay ?? DateTime.Now.Month;

            // Birimler listesi getir (MesaiController ile aynı şekilde)
            var birimler = new List<string>();
            await using var conn = await _dbFactory.CreateConnectionAsync();
            
            if (rol == "admin")
            {
                // Admin ise tüm birimler
                var query = "SELECT birim_adi FROM birimler ORDER BY birim_adi";
                try
                {
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                if (!reader.IsDBNull(0))
                                    birimler.Add(reader.GetString(0));
                            }
                        }
                    }
                }
                catch (MySqlException ex)
                {
                    _logger.LogWarning($"Birimler sorgusu başarısız: {ex.Message}");
                }
            }
            else if (!string.IsNullOrEmpty(kullaniciBirim))
            {
                // Admin değilse sadece kendi birimi
                birimler.Add(kullaniciBirim);
            }

            // Birim seçimi: URL'den gelen veya session'dan veya ilk birim
            if (string.IsNullOrEmpty(birim) && !string.IsNullOrEmpty(kullaniciBirim))
                birim = kullaniciBirim;
            if (string.IsNullOrEmpty(birim) && birimler.Count > 0)
                birim = birimler[0];

            ViewBag.Birimler = birimler;
            ViewBag.SecilenBirim = birim ?? "";
            ViewBag.Rol = rol;

            // Birim seçilmişse, o birime ait personelleri getir
            var personeller = new List<dynamic>();
            var personelMesaiVerileri = new Dictionary<int, Dictionary<string, dynamic>>();

            if (!string.IsNullOrEmpty(birim))
            {
                // Seçilen birimin personellerini getir (MesaiController'daki gibi)
                var personelQuery = "SELECT id, ad, soyad, per_statu FROM personeller WHERE birim_adi = @birim ORDER BY ad, soyad";

                try
                {
                    using (var cmd = new MySqlCommand(personelQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@birim", birim);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                personeller.Add(new
                                {
                                    Id = reader.GetInt32("id"),
                                    Ad = reader.IsDBNull(1) ? "" : reader.GetString("ad"),
                                    Soyad = reader.IsDBNull(2) ? "" : reader.GetString("soyad"),
                                    AdSoyad = (reader.IsDBNull(1) ? "" : reader.GetString("ad")) + " " + (reader.IsDBNull(2) ? "" : reader.GetString("soyad")),
                                    GorevAdi = reader.IsDBNull(3) ? "" : reader.GetString("per_statu")
                                });
                            }
                        }
                    }
                }
                catch (MySqlException ex)
                {
                    _logger.LogWarning($"Personel sorgusu başarısız: {ex.Message}");
                }

                // Mesai verilerini getir (personeller.id ile mesai_kayitlari.personel_id arasında)
                var mesaiQuery = @"
                    SELECT mk.id, mk.personel_id, mk.tarih, mk.baslangic, mk.bitis, 
                           mk.toplam_saat, mk.aciklama
                    FROM mesai_kayitlari mk
                    WHERE mk.personel_id IN (
                        SELECT id FROM personeller WHERE birim_adi = @birim
                    )
                    AND YEAR(mk.tarih) = @Yil 
                    AND MONTH(mk.tarih) = @Ay
                    ORDER BY mk.tarih ASC";

                try
                {
                    using (var cmd = new MySqlCommand(mesaiQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@birim", birim);
                        cmd.Parameters.AddWithValue("@Yil", yil);
                        cmd.Parameters.AddWithValue("@Ay", ay);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var personelId = reader.GetInt32("personel_id");
                                var tarihStr = reader["tarih"]?.ToString() ?? "";
                                if (!DateTime.TryParse(tarihStr, out var tarih))
                                {
                                    _logger.LogWarning("Geçersiz tarih formatı atlandı: {Tarih}", tarihStr);
                                    continue;
                                }
                                var gun = tarih.Day;

                                if (!personelMesaiVerileri.ContainsKey(personelId))
                                    personelMesaiVerileri[personelId] = new Dictionary<string, dynamic>();

                                personelMesaiVerileri[personelId][gun.ToString()] = new
                                {
                                    Id = reader.GetInt32("id"),
                                    BaslangicSaati = reader["baslangic"]?.ToString() ?? "",
                                    BitisSaati = reader["bitis"]?.ToString() ?? "",
                                    ToplamSaat = Convert.ToDecimal(reader["toplam_saat"]),
                                    Notlar = reader["aciklama"]?.ToString() ?? ""
                                };
                            }
                        }
                    }
                }
                catch (MySqlException ex)
                {
                    _logger.LogWarning($"Mesai kayıtları sorgusu başarısız: {ex.Message}");
                }
            }

            // Ayın gün sayısını hesapla
            int gunSayisi = DateTime.DaysInMonth(yil.Value, ay.Value);

            ViewBag.Personeller = personeller;
            ViewBag.PersonelMesaiVerileri = personelMesaiVerileri;
            ViewBag.SecilenYil = yil;
            ViewBag.SecilenAy = ay;
            ViewBag.GunSayisi = gunSayisi;
            ViewBag.Rol = rol;

            return View("~/Views/PRS/Mesai/HizliMesaiGirisi/Index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError($"HizliMesaiGirisi Index Hatası: {ex.Message}");
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // POST: HizliMesaiGirisi/KayitEkle - Mesai kaydı ekleme
    [HttpPost]
    public async Task<IActionResult> KayitEkle([FromBody] MesaiKayitModel model)
    {
        if (model == null)
            return BadRequest("Geçersiz istek verisi");

        if (model.PersonelId <= 0)
            return Unauthorized();

        try
        {
            // Saat validasyonu
            if (!TimeSpan.TryParse(model.BaslangicSaati, out var baslangic) || 
                !TimeSpan.TryParse(model.BitisSaati, out var bitis))
                return BadRequest("Geçersiz saat formatı");

            if (bitis <= baslangic)
                return BadRequest("Bitiş saati, başlangıç saatinden sonra olmalıdır");

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                // Aynı tarihte kaydın olup olmadığını kontrol et
                var checkQuery = "SELECT COUNT(*) FROM mesai_kayitlari WHERE personel_id = @PersonelId AND tarih = @Tarih";
                int count = 0;
                try
                {
                    using (var checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@PersonelId", model.PersonelId);
                        checkCmd.Parameters.AddWithValue("@Tarih", model.Tarih);
                        count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync() ?? 0);
                    }
                }
                catch (MySqlException ex)
                {
                    _logger.LogWarning($"Kayıt kontrolü başarısız: {ex.Message}");
                    // Tablo yoksa kontrol et ama hata verme
                }

                if (count > 0)
                    return BadRequest("Bu tarihte zaten mesai kaydı var");

                // Yeni mesai kaydı ekle
                var toplamSaat = Math.Round((decimal)(bitis - baslangic).TotalHours, 2);

                var insertQuery = @"
                    INSERT INTO mesai_kayitlari (personel_id, tarih, baslangic, bitis, toplam_saat, aciklama)
                    VALUES (@PersonelId, @Tarih, @Baslangic, @Bitis, @ToplamSaat, @Aciklama)";

                try
                {
                    using (var cmd = new MySqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@PersonelId", model.PersonelId);
                        cmd.Parameters.AddWithValue("@Tarih", model.Tarih);
                        cmd.Parameters.AddWithValue("@Baslangic", model.BaslangicSaati);
                        cmd.Parameters.AddWithValue("@Bitis", model.BitisSaati);
                        cmd.Parameters.AddWithValue("@ToplamSaat", toplamSaat);
                        cmd.Parameters.AddWithValue("@Aciklama", model.Notlar ?? "");

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                catch (MySqlException ex)
                {
                    _logger.LogError($"Mesai kaydı ekleme hatası: {ex.Message}");
                    return BadRequest($"Veritabanı hatası: {ex.Message}");
                }
            }

            return Ok(new { success = true, message = "Mesai kaydı başarıyla eklendi" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"KayitEkle Hatası: {ex.Message}");
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // POST: HizliMesaiGirisi/Sil - Mesai kaydı silme
    [HttpPost]
    public async Task<IActionResult> Sil([FromBody] int id)
    {
        if (id <= 0)
            return Unauthorized();

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                // Kaydın sahibi olup olmadığını kontrol et
                // Kaydı sil
                var deleteQuery = "DELETE FROM mesai_kayitlari WHERE id = @Id";
                using (var cmd = new MySqlCommand(deleteQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return Ok(new { success = true, message = "Mesai kaydı silindi" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Sil Hatası: {ex.Message}");
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // POST: HizliMesaiGirisi/Guncelle - Mesai kaydı güncelleme
    [HttpPost]
    public async Task<IActionResult> Guncelle([FromBody] MesaiKayitUpdateModel model)
    {
        if (model.Id <= 0)
            return Unauthorized();

        if (!TimeSpan.TryParse(model.BaslangicSaati, out var baslangic) ||
            !TimeSpan.TryParse(model.BitisSaati, out var bitis))
            return BadRequest("Geçersiz saat formatı");

        if (bitis <= baslangic)
            return BadRequest("Bitiş saati, başlangıç saatinden sonra olmalıdır");

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                // Kaydı güncelle
                var toplamSaat = Math.Round((decimal)(bitis - baslangic).TotalHours, 2);
                var updateQuery = @"
                    UPDATE mesai_kayitlari 
                    SET baslangic = @Baslangic, bitis = @Bitis, toplam_saat = @ToplamSaat, aciklama = @Aciklama
                    WHERE id = @Id";

                using (var cmd = new MySqlCommand(updateQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", model.Id);
                    cmd.Parameters.AddWithValue("@Baslangic", model.BaslangicSaati);
                    cmd.Parameters.AddWithValue("@Bitis", model.BitisSaati);
                    cmd.Parameters.AddWithValue("@ToplamSaat", toplamSaat);
                    cmd.Parameters.AddWithValue("@Aciklama", model.Notlar ?? "");

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return Ok(new { success = true, message = "Mesai kaydı güncellendi" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Guncelle Hatası: {ex.Message}");
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // GET: HizliMesaiGirisi/Admin - Admin onay paneli
    public async Task<IActionResult> Admin(string? subeAdi = null, string? durum = null)
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";

        if (rol != "Admin")
            return RedirectToAction("Index", "Home");

        try
        {
            var bekleyenKayitlar = new List<dynamic>();
            var subeler = new List<dynamic>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                // Tüm birimleri al
                var subeQuery = "SELECT DISTINCT birim_adi AS Ad FROM personeller ORDER BY birim_adi";
                try
                {
                    using (var subeCmd = new MySqlCommand(subeQuery, conn))
                    using (var reader = await subeCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                            subeler.Add(new { Ad = reader.GetString("Ad") });
                    }
                }
                catch (MySqlException ex)
                {
                    _logger.LogWarning($"Şube listesi sorgusu başarısız: {ex.Message}");
                }

                // Mesai kayıtlarını al
                var query = @"
                    SELECT mk.id, mk.personel_id, 
                           CONCAT(COALESCE(p.ad, ''), ' ', COALESCE(p.soyad, '')) AS AdSoyad,
                           p.birim_adi AS SubeAdi,
                           mk.tarih, mk.baslangic, mk.bitis, mk.toplam_saat, mk.aciklama
                    FROM mesai_kayitlari mk
                    LEFT JOIN personeller p ON mk.personel_id = p.id
                    WHERE 1=1";

                if (!string.IsNullOrWhiteSpace(subeAdi))
                    query += " AND p.birim_adi = @SubeAdi";

                query += " ORDER BY mk.tarih DESC";

                try
                {
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        if (!string.IsNullOrWhiteSpace(subeAdi))
                            cmd.Parameters.AddWithValue("@SubeAdi", subeAdi);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                bekleyenKayitlar.Add(new
                                {
                                    Id = reader.GetInt32("id"),
                                    AdSoyad = reader["AdSoyad"]?.ToString() ?? "",
                                    SubeAdi = reader["SubeAdi"]?.ToString() ?? "",
                                    Tarih = Convert.ToDateTime(reader["tarih"]),
                                    BaslangicSaati = reader["baslangic"]?.ToString() ?? "",
                                    BitisSaati = reader["bitis"]?.ToString() ?? "",
                                    ToplamSaat = Convert.ToDecimal(reader["toplam_saat"]),
                                    Durum = "Mesai",
                                    Notlar = reader["aciklama"]?.ToString() ?? "",
                                    OlusturmaTarihi = Convert.ToDateTime(reader["tarih"])
                                });
                            }
                        }
                    }
                }
                catch (MySqlException ex)
                {
                    _logger.LogWarning($"Mesai kayıtları sorgusu başarısız: {ex.Message}");
                }
            }

            ViewBag.BekleyenKayitlar = bekleyenKayitlar;
            ViewBag.Subeler = subeler;
            ViewBag.SeciiliSube = subeAdi ?? "";
            ViewBag.SeciliDurum = durum ?? "Bekliyor";

            return View("~/Views/PRS/Mesai/HizliMesaiGirisi/Admin.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Admin Hatası: {ex.Message}");
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // POST: HizliMesaiGirisi/Onayla - Mesai kaydını onaylama (Admin)
    [HttpPost]
    public async Task<IActionResult> Onayla([FromBody] OnayModel model)
    {
        return BadRequest("Bu sayfada onay akışı kapatıldı. Sayfa sadece mesai kaydı içindir.");
    }

    // GET: HizliMesaiGirisi/GetAyVerileri - AJAX için ay verilerini getir
    [HttpGet]
    public async Task<IActionResult> GetAyVerileri(int yil, int ay)
    {
        var personelId = HttpContext.Session.GetInt32("PersonelId") ?? HttpContext.Session.GetInt32("KullaniciId") ?? 0;

        if (personelId == 0)
            return Unauthorized();

        try
        {
            var ayMesaiVerileri = new Dictionary<int, dynamic>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                var query = @"
                    SELECT mk.id, mk.tarih, mk.baslangic, mk.bitis, mk.toplam_saat, mk.aciklama
                    FROM mesai_kayitlari mk
                    WHERE mk.personel_id = @PersonelId 
                    AND YEAR(mk.tarih) = @Yil 
                    AND MONTH(mk.tarih) = @Ay
                    ORDER BY mk.tarih ASC";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PersonelId", personelId);
                    cmd.Parameters.AddWithValue("@Yil", yil);
                    cmd.Parameters.AddWithValue("@Ay", ay);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var tarihStr = reader["tarih"]?.ToString() ?? "";
                            if (!DateTime.TryParse(tarihStr, out var tarih))
                            {
                                _logger.LogWarning("GetAyVerileri: Geçersiz tarih formatı atlandı: {Tarih}", tarihStr);
                                continue;
                            }
                            var gun = tarih.Day;

                            ayMesaiVerileri[gun] = new
                            {
                                Id = reader.GetInt32("id"),
                                Tarih = tarih.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                                BaslangicSaati = reader["baslangic"]?.ToString() ?? "",
                                BitisSaati = reader["bitis"]?.ToString() ?? "",
                                ToplamSaat = Convert.ToDecimal(reader["toplam_saat"]),
                                Notlar = reader["aciklama"]?.ToString() ?? ""
                            };
                        }
                    }
                }
            }

            return Json(ayMesaiVerileri);
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetAyVerileri Hatası: {ex.Message}");
            return BadRequest(ex.Message);
        }
    }

    // POST: HizliMesaiGirisi/ImportExcel - Excel/CSV dosyası yükle
    [HttpPost]
    public async Task<IActionResult> ImportExcel(IFormFile file)
    {
        var personelId = HttpContext.Session.GetInt32("PersonelId") ?? HttpContext.Session.GetInt32("KullaniciId") ?? 0;

        if (personelId == 0)
            return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest("Lütfen dosya seçiniz");

        try
        {
            var kayitlar = new List<(DateTime tarih, string baslangic, string bitis, string notlar)>();
            int basariSayisi = 0;
            int hataSayisi = 0;

            using (var stream = file.OpenReadStream())
            using (var reader = new StreamReader(stream))
            {
                string? line;
                int lineNumber = 0;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;
                    if (lineNumber == 1) continue; // Başlık satırını atla

                    var parts = line.Split(',');
                    if (parts.Length < 3)
                        continue;

                    if (DateTime.TryParse(parts[0].Trim(), out var tarih) &&
                        TimeSpan.TryParse(parts[1].Trim(), out var baslangic) &&
                        TimeSpan.TryParse(parts[2].Trim(), out var bitis))
                    {
                        var notlar = parts.Length > 3 ? parts[3].Trim() : "";
                        kayitlar.Add((tarih, baslangic.ToString(@"hh\:mm"), bitis.ToString(@"hh\:mm"), notlar));
                    }
                    else
                    {
                        hataSayisi++;
                    }
                }
            }

            // Veritabanına ekle
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                var insertQuery = @"
                    INSERT INTO mesai_kayitlari (personel_id, tarih, baslangic, bitis, toplam_saat, aciklama)
                    VALUES (@PersonelId, @Tarih, @Baslangic, @Bitis, @ToplamSaat, @Aciklama)";

                foreach (var (tarih, baslangic, bitis, notlar) in kayitlar)
                {
                    try
                    {
                        using (var cmd = new MySqlCommand(insertQuery, conn))
                        {
                            var toplamSaat = Math.Round((decimal)(TimeSpan.Parse(bitis) - TimeSpan.Parse(baslangic)).TotalHours, 2);

                            cmd.Parameters.AddWithValue("@PersonelId", personelId);
                            cmd.Parameters.AddWithValue("@Tarih", tarih);
                            cmd.Parameters.AddWithValue("@Baslangic", baslangic);
                            cmd.Parameters.AddWithValue("@Bitis", bitis);
                            cmd.Parameters.AddWithValue("@ToplamSaat", toplamSaat);
                            cmd.Parameters.AddWithValue("@Aciklama", notlar);

                            await cmd.ExecuteNonQueryAsync();
                            basariSayisi++;
                        }
                    }
                    catch
                    {
                        hataSayisi++;
                    }
                }
            }

            return Ok(new { 
                success = true, 
                message = $"{basariSayisi} kayıt eklendi, {hataSayisi} hata" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"ImportExcel Hatası: {ex.Message}");
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // GET: HizliMesaiGirisi/GetAyToplami - Ay ve sınır kontrolü
    [HttpGet]
    public async Task<IActionResult> GetAyToplami(int yil, int ay)
    {
        var personelId = HttpContext.Session.GetInt32("PersonelId") ?? HttpContext.Session.GetInt32("KullaniciId") ?? 0;

        if (personelId == 0)
            return Unauthorized();

        try
        {
            var ayToplami = 0m;
            var limitAsildiMi = false;

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                var query = @"
                    SELECT COALESCE(SUM(mk.toplam_saat), 0) as Toplam
                    FROM mesai_kayitlari mk
                    WHERE mk.personel_id = @PersonelId 
                    AND YEAR(mk.tarih) = @Yil 
                    AND MONTH(mk.tarih) = @Ay";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PersonelId", personelId);
                    cmd.Parameters.AddWithValue("@Yil", yil);
                    cmd.Parameters.AddWithValue("@Ay", ay);

                    ayToplami = Convert.ToDecimal(await cmd.ExecuteScalarAsync() ?? 0m);
                }

                // 20 saat limitini kontrol et
                limitAsildiMi = ayToplami > 20m;
            }

            return Json(new { ayToplami, limitAsildiMi });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

// Model sınıfları
public class MesaiKayitModel
{
    public int PersonelId { get; set; }
    public string Tarih { get; set; } = string.Empty;
    public string BaslangicSaati { get; set; } = string.Empty;
    public string BitisSaati { get; set; } = string.Empty;
    public string? Notlar { get; set; }
}

public class MesaiKayitUpdateModel
{
    public int Id { get; set; }
    public string BaslangicSaati { get; set; } = string.Empty;
    public string BitisSaati { get; set; } = string.Empty;
    public string? Notlar { get; set; }
}

public class OnayModel
{
    public int Id { get; set; }
    public string Durum { get; set; } = "Onaylandi"; // "Onaylandi" veya "Reddedildi"
}
