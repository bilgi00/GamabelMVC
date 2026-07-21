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

    // ============================================================
    // GET: HizliMesaiGirisi/Index - Ana takvim sayfası
    // ============================================================
    public async Task<IActionResult> Index(int? yil = null, int? ay = null, string? birim = null)
    {
        var kullaniciId = HttpContext.Session.GetInt32("PersonelId") ?? HttpContext.Session.GetInt32("KullaniciId") ?? 1;
        var rol = HttpContext.Session.GetString("Rol") ?? "birim_amiri";
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";

        try
        {
            yil = yil ?? DateTime.Now.Year;
            ay = ay ?? DateTime.Now.Month;

            var birimler = new List<string>();
            await using var conn = await _dbFactory.CreateConnectionAsync();
            
            if (rol == "admin")
            {
                try
                {
                    var query = "SELECT birim_adi FROM birimler ORDER BY birim_adi";
                    using (var cmd = new MySqlCommand(query, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            if (!reader.IsDBNull(0))
                                birimler.Add(reader.GetString(0));
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
                birimler.Add(kullaniciBirim);
            }

            if (string.IsNullOrEmpty(birim) && !string.IsNullOrEmpty(kullaniciBirim))
                birim = kullaniciBirim;
            if (string.IsNullOrEmpty(birim) && birimler.Count > 0)
                birim = birimler[0];

            ViewBag.Birimler = birimler;
            ViewBag.SecilenBirim = birim ?? "";
            ViewBag.Rol = rol;

            var personeller = new List<dynamic>();
            var personelMesaiVerileri = new Dictionary<int, Dictionary<string, dynamic>>();

            if (!string.IsNullOrEmpty(birim))
            {
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
                                var ad = reader.IsDBNull(1) ? "" : reader.GetString("ad");
                                var soyad = reader.IsDBNull(2) ? "" : reader.GetString("soyad");
                                
                                personeller.Add(new
                                {
                                    Id = reader.GetInt32("id"),
                                    Ad = ad,
                                    Soyad = soyad,
                                    AdSoyad = ad + " " + soyad,
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

                var mesaiQuery = @"
                    SELECT mk.id, mk.personel_id, mk.tarih, 
                           mk.toplam_saat, mk.aciklama,
                           mk.baslangic, mk.bitis
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
                        cmd.Parameters.AddWithValue("@Yil", yil.Value);
                        cmd.Parameters.AddWithValue("@Ay", ay.Value);

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

                                var toplamSaat = Convert.ToDecimal(reader["toplam_saat"]);
                                var toplamSaatStr = toplamSaat.ToString(CultureInfo.InvariantCulture);

                                personelMesaiVerileri[personelId][gun.ToString()] = new
                                {
                                    Id = reader.GetInt32("id"),
                                    BaslangicSaati = reader["baslangic"]?.ToString() ?? "",
                                    BitisSaati = reader["bitis"]?.ToString() ?? "",
                                    ToplamSaat = toplamSaatStr,
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
            TempData["Hata"] = "Veri yüklenirken bir hata oluştu: " + ex.Message;
            return View("~/Views/PRS/Mesai/HizliMesaiGirisi/Index.cshtml");
        }
    }

    // ============================================================
    // POST: HizliMesaiGirisi/KayitEkle - Mesai kaydı ekleme ✅ DÜZELTİLDİ
    // ============================================================
    [HttpPost]
    public async Task<IActionResult> KayitEkle([FromBody] MesaiKayitModel model)
    {
        // 1. Model kontrolü
        if (model == null)
            return BadRequest("Geçersiz istek verisi");

        // 2. Personel ID kontrolü
        if (model.PersonelId <= 0)
            return BadRequest("Geçersiz personel ID");

        // 3. Tarih kontrolü
        if (string.IsNullOrEmpty(model.Tarih))
            return BadRequest("Tarih alanı zorunludur");

        try
        {
            // 4. Tarih parse
            if (!DateTime.TryParse(model.Tarih, out var tarih))
                return BadRequest("Geçersiz tarih formatı");

            // 5. Saat validasyonu
            if (string.IsNullOrEmpty(model.BaslangicSaati))
                return BadRequest("Başlangıç saati zorunludur");
            
            if (string.IsNullOrEmpty(model.BitisSaati))
                return BadRequest("Bitiş saati zorunludur");

            if (!TimeSpan.TryParse(model.BaslangicSaati, out var baslangic))
                return BadRequest($"Geçersiz başlangıç saati formatı: {model.BaslangicSaati}");

            if (!TimeSpan.TryParse(model.BitisSaati, out var bitis))
                return BadRequest($"Geçersiz bitiş saati formatı: {model.BitisSaati}");

            // 6. Saat kontrolü
            if (bitis <= baslangic)
                return BadRequest("Bitiş saati, başlangıç saatinden sonra olmalıdır");

            if ((bitis - baslangic).TotalHours <= 0)
                return BadRequest("Geçerli bir süre giriniz");

            if ((bitis - baslangic).TotalHours > 12)
                return BadRequest("Tek seferde maksimum 12 saat mesai girilebilir");

            // 7. Veritabanı işlemleri
            await using var conn = await _dbFactory.CreateConnectionAsync();

            // Aynı tarihte kayıt var mı?
            var checkQuery = "SELECT COUNT(*) FROM mesai_kayitlari WHERE personel_id = @PersonelId AND DATE(tarih) = @Tarih";
            int count = 0;
            try
            {
                using (var checkCmd = new MySqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@PersonelId", model.PersonelId);
                    checkCmd.Parameters.AddWithValue("@Tarih", tarih.Date);
                    count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync() ?? 0);
                }
            }
            catch (MySqlException ex)
            {
                _logger.LogWarning($"Kayıt kontrolü başarısız: {ex.Message}");
                // Tablo yoksa veya sorgu başarısızsa devam et
            }

            if (count > 0)
                return BadRequest("Bu tarihte zaten mesai kaydı var");

            // Yeni kayıt ekle
            var toplamSaat = Math.Round((decimal)(bitis - baslangic).TotalHours, 2);

            var insertQuery = @"
                INSERT INTO mesai_kayitlari (personel_id, tarih, baslangic, bitis, toplam_saat, aciklama)
                VALUES (@PersonelId, @Tarih, @Baslangic, @Bitis, @ToplamSaat, @Aciklama)";

            using (var cmd = new MySqlCommand(insertQuery, conn))
            {
                cmd.Parameters.AddWithValue("@PersonelId", model.PersonelId);
                cmd.Parameters.AddWithValue("@Tarih", tarih.Date);
                cmd.Parameters.AddWithValue("@Baslangic", baslangic.ToString(@"hh\:mm"));
                cmd.Parameters.AddWithValue("@Bitis", bitis.ToString(@"hh\:mm"));
                cmd.Parameters.AddWithValue("@ToplamSaat", toplamSaat);
                cmd.Parameters.AddWithValue("@Aciklama", model.Notlar ?? "");

                await cmd.ExecuteNonQueryAsync();
            }

            return Ok(new { success = true, message = "Mesai kaydı başarıyla eklendi" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"KayitEkle Hatası: {ex.Message}");
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // ============================================================
    // POST: HizliMesaiGirisi/Guncelle - Mesai kaydı güncelleme
    // ============================================================
    [HttpPost]
    public async Task<IActionResult> Guncelle([FromBody] MesaiKayitUpdateModel model)
    {
        if (model == null || model.Id <= 0)
            return BadRequest("Geçersiz kayıt ID");

        try
        {
            if (!TimeSpan.TryParse(model.BaslangicSaati, out var baslangic) ||
                !TimeSpan.TryParse(model.BitisSaati, out var bitis))
                return BadRequest("Geçersiz saat formatı (HH:MM)");

            if (bitis <= baslangic)
                return BadRequest("Bitiş saati, başlangıç saatinden sonra olmalıdır");

            if ((bitis - baslangic).TotalHours > 12)
                return BadRequest("Tek seferde maksimum 12 saat mesai girilebilir");

            await using var conn = await _dbFactory.CreateConnectionAsync();

            var toplamSaat = Math.Round((decimal)(bitis - baslangic).TotalHours, 2);
            var updateQuery = @"
                UPDATE mesai_kayitlari 
                SET baslangic = @Baslangic, bitis = @Bitis, toplam_saat = @ToplamSaat, aciklama = @Aciklama
                WHERE id = @Id";

            using (var cmd = new MySqlCommand(updateQuery, conn))
            {
                cmd.Parameters.AddWithValue("@Id", model.Id);
                cmd.Parameters.AddWithValue("@Baslangic", baslangic.ToString(@"hh\:mm"));
                cmd.Parameters.AddWithValue("@Bitis", bitis.ToString(@"hh\:mm"));
                cmd.Parameters.AddWithValue("@ToplamSaat", toplamSaat);
                cmd.Parameters.AddWithValue("@Aciklama", model.Notlar ?? "");

                await cmd.ExecuteNonQueryAsync();
            }

            return Ok(new { success = true, message = "Mesai kaydı güncellendi" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Guncelle Hatası: {ex.Message}");
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // ============================================================
    // POST: HizliMesaiGirisi/Sil - Mesai kaydı silme
    // ============================================================
    [HttpPost]
    public async Task<IActionResult> Sil([FromBody] int id)
    {
        if (id <= 0)
            return BadRequest("Geçersiz kayıt ID");

        try
        {
            await using var conn = await _dbFactory.CreateConnectionAsync();

            var deleteQuery = "DELETE FROM mesai_kayitlari WHERE id = @Id";
            using (var cmd = new MySqlCommand(deleteQuery, conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                await cmd.ExecuteNonQueryAsync();
            }

            return Ok(new { success = true, message = "Mesai kaydı silindi" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Sil Hatası: {ex.Message}");
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // ============================================================
    // GET: HizliMesaiGirisi/GetAyVerileri - AJAX için ay verileri
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> GetAyVerileri(int yil, int ay, int? personelId = null)
    {
        var pid = personelId ?? HttpContext.Session.GetInt32("PersonelId") ?? HttpContext.Session.GetInt32("KullaniciId") ?? 0;

        if (pid == 0)
            return Unauthorized("Personel bilgisi bulunamadı");

        try
        {
            var ayMesaiVerileri = new Dictionary<int, dynamic>();

            await using var conn = await _dbFactory.CreateConnectionAsync();

            var query = @"
                SELECT mk.id, mk.tarih, mk.baslangic, mk.bitis, mk.toplam_saat, mk.aciklama
                FROM mesai_kayitlari mk
                WHERE mk.personel_id = @PersonelId 
                AND YEAR(mk.tarih) = @Yil 
                AND MONTH(mk.tarih) = @Ay
                ORDER BY mk.tarih ASC";

            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@PersonelId", pid);
                cmd.Parameters.AddWithValue("@Yil", yil);
                cmd.Parameters.AddWithValue("@Ay", ay);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var tarihStr = reader["tarih"]?.ToString() ?? "";
                        if (!DateTime.TryParse(tarihStr, out var tarih))
                            continue;

                        var gun = tarih.Day;
                        var toplamSaat = Convert.ToDecimal(reader["toplam_saat"]);

                        ayMesaiVerileri[gun] = new
                        {
                            Id = reader.GetInt32("id"),
                            Tarih = tarih.ToString("yyyy-MM-dd"),
                            BaslangicSaati = reader["baslangic"]?.ToString() ?? "",
                            BitisSaati = reader["bitis"]?.ToString() ?? "",
                            ToplamSaat = toplamSaat.ToString(CultureInfo.InvariantCulture),
                            Notlar = reader["aciklama"]?.ToString() ?? ""
                        };
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

    // ============================================================
    // GET: HizliMesaiGirisi/GetAyToplami - Ay toplamı ve limit kontrolü
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> GetAyToplami(int yil, int ay, int? personelId = null)
    {
        var pid = personelId ?? HttpContext.Session.GetInt32("PersonelId") ?? HttpContext.Session.GetInt32("KullaniciId") ?? 0;

        if (pid == 0)
            return Unauthorized("Personel bilgisi bulunamadı");

        try
        {
            await using var conn = await _dbFactory.CreateConnectionAsync();

            var query = @"
                SELECT COALESCE(SUM(mk.toplam_saat), 0) as Toplam
                FROM mesai_kayitlari mk
                WHERE mk.personel_id = @PersonelId 
                AND YEAR(mk.tarih) = @Yil 
                AND MONTH(mk.tarih) = @Ay";

            decimal ayToplami;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@PersonelId", pid);
                cmd.Parameters.AddWithValue("@Yil", yil);
                cmd.Parameters.AddWithValue("@Ay", ay);

                ayToplami = Convert.ToDecimal(await cmd.ExecuteScalarAsync() ?? 0m);
            }

            var limitAsildiMi = ayToplami > 88m;

            return Json(new { 
                ayToplami = ayToplami.ToString("F2", CultureInfo.InvariantCulture),
                limitAsildiMi 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetAyToplami Hatası: {ex.Message}");
            return BadRequest(ex.Message);
        }
    }

    // ============================================================
    // POST: HizliMesaiGirisi/ImportExcel - Excel/CSV dosyası yükle
    // ============================================================
    [HttpPost]
    public async Task<IActionResult> ImportExcel(IFormFile file)
    {
        var personelId = HttpContext.Session.GetInt32("PersonelId") ?? HttpContext.Session.GetInt32("KullaniciId") ?? 0;

        if (personelId == 0)
            return Unauthorized("Personel bilgisi bulunamadı");

        if (file == null || file.Length == 0)
            return BadRequest("Lütfen dosya seçiniz");

        try
        {
            var kayitlar = new List<(DateTime tarih, TimeSpan baslangic, TimeSpan bitis, string notlar)>();
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
                    if (lineNumber == 1) continue;

                    var parts = line.Split(',');
                    if (parts.Length < 3) continue;

                    if (DateTime.TryParse(parts[0].Trim(), out var tarih) &&
                        TimeSpan.TryParse(parts[1].Trim(), out var baslangic) &&
                        TimeSpan.TryParse(parts[2].Trim(), out var bitis))
                    {
                        var notlar = parts.Length > 3 ? parts[3].Trim() : "";
                        kayitlar.Add((tarih, baslangic, bitis, notlar));
                    }
                    else
                    {
                        hataSayisi++;
                    }
                }
            }

            await using var conn = await _dbFactory.CreateConnectionAsync();

            var insertQuery = @"
                INSERT INTO mesai_kayitlari (personel_id, tarih, baslangic, bitis, toplam_saat, aciklama)
                VALUES (@PersonelId, @Tarih, @Baslangic, @Bitis, @ToplamSaat, @Aciklama)";

            foreach (var (tarih, baslangic, bitis, notlar) in kayitlar)
            {
                try
                {
                    using (var cmd = new MySqlCommand(insertQuery, conn))
                    {
                        var toplamSaat = Math.Round((decimal)(bitis - baslangic).TotalHours, 2);

                        cmd.Parameters.AddWithValue("@PersonelId", personelId);
                        cmd.Parameters.AddWithValue("@Tarih", tarih.Date);
                        cmd.Parameters.AddWithValue("@Baslangic", baslangic.ToString(@"hh\:mm"));
                        cmd.Parameters.AddWithValue("@Bitis", bitis.ToString(@"hh\:mm"));
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

    // ============================================================
    // GET: HizliMesaiGirisi/Admin - Admin onay paneli
    // ============================================================
    public async Task<IActionResult> Admin(string? subeAdi = null, string? durum = null)
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";

        if (rol != "admin" && rol != "Admin")
            return RedirectToAction("Index", "Home");

        try
        {
            var bekleyenKayitlar = new List<dynamic>();
            var subeler = new List<dynamic>();

            await using var conn = await _dbFactory.CreateConnectionAsync();

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
                                Notlar = reader["aciklama"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                _logger.LogWarning($"Mesai kayıtları sorgusu başarısız: {ex.Message}");
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

    // ============================================================
    // Model Sınıfları
    // ============================================================
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
        public string Durum { get; set; } = "Onaylandi";
    }
}