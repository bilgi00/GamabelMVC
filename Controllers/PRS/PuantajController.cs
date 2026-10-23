using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Data;
using System.Globalization;
using MySqlConnector;
using gamabelmvc.Models.PRS;
using gamabelmvc.Services;
using gamabelmvc.Services.Raporlama;

namespace gamabelmvc.Controllers.PRS;

[Authorize(Policy = "PrsMenuPuantaj")]
public class PuantajController : Controller
{
    private readonly string _connectionString;
    private readonly IRaporServisi _raporServisi;

    private static readonly RaporTanimi PuantajRaporTanimi = RaporKaydi.Puantaj;

    public PuantajController(IConfiguration configuration, IRaporServisi raporServisi)
    {
        _connectionString = configuration.GetConnectionString("MyConnection")!;
        _raporServisi = raporServisi;
    }

    public async Task<IActionResult> Index()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return RedirectToAction("Login", "Account");

        var rol = HttpContext.Session.GetString("Rol") ?? "birim_amiri";
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";

        var birimler = new List<string>();
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            if (rol == "admin")
            {
                await using var cmd = new MySqlCommand("SELECT birim_adi FROM birimler ORDER BY birim_adi", connection);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                        birimler.Add(reader.GetString(0));
                }
            }
            else if (!string.IsNullOrEmpty(kullaniciBirim))
            {
                birimler.Add(kullaniciBirim);
            }
        }
        catch (Exception ex)
        {
            ViewBag.Hata = "Veritabanı hatası: " + ex.Message;
        }

        ViewBag.Birimler = birimler;
        ViewBag.KullaniciAdi = HttpContext.Session.GetString("KullaniciAdi");
        ViewBag.Rol = rol;
        ViewBag.KullaniciBirim = kullaniciBirim;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> AyKilitliMi(int yil, int ay)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();
        if (yil < 1 || ay is < 1 or > 12)
            return BadRequest(new { mesaj = "Geçersiz yıl veya ay." });

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return Json(new { kilitli = await AyKilitKontrol.KilitliMiAsync(connection, yil, ay) });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { mesaj = "Ay kilit durumu kontrol edilemedi.", hata = ex.Message });
        }
    }

    // Birime göre personel listesi
    [HttpGet]
    public async Task<IActionResult> GetPersoneller(string birim)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        var rol = HttpContext.Session.GetString("Rol") ?? "birim_amiri";
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";

        // Admin değilse, sadece kendi birimini görebilir
        if (rol != "admin" && !string.IsNullOrEmpty(kullaniciBirim))
        {
            birim = kullaniciBirim;
        }

        var personeller = new List<object>();
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = (rol == "admin" && birim == "all")
                ? "SELECT per_no, ad, soyad, birim_adi, per_statu FROM personeller ORDER BY birim_adi, ad, soyad"
                : "SELECT per_no, ad, soyad, birim_adi, per_statu FROM personeller WHERE birim_adi = @birim ORDER BY ad, soyad";

            await using var cmd = new MySqlCommand(sql, connection);
            if (!(rol == "admin" && birim == "all"))
                cmd.Parameters.AddWithValue("@birim", birim);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                personeller.Add(new
                {
                    personelId = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0)) ?? "",
                    perNo = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0)) ?? "",
                    ad = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    soyad = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    birim = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    statu = reader.IsDBNull(4) ? "" : reader.GetString(4)
                });
            }
        }
        catch (Exception ex)
        {
            return Json(new { hata = ex.Message });
        }

        return Json(personeller);
    }

    // Belirli ay/yıl için izin kayıtlarını getir
    [HttpGet]
    public async Task<IActionResult> GetIzinler(int yil, int ay)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        var izinler = new List<object>();
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var cmd = new MySqlCommand(
                @"SELECT pi.id, pi.personel_id, p.per_no, p.ad, p.soyad,
                         pi.gun, pi.izin_tipi, pi.aciklama
                  FROM puantaj_izin pi
                  INNER JOIN personeller p ON p.per_no = pi.personel_id
                  WHERE pi.yil = @yil AND pi.ay = @ay",
                connection);
            cmd.Parameters.AddWithValue("@yil", yil);
            cmd.Parameters.AddWithValue("@ay", ay);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var izinTipi = NormalizePuantajDurumu(reader.IsDBNull(6) ? null : reader.GetString(6));

                izinler.Add(new
                {
                    id = reader.GetInt32(0),
                    personelId = reader.IsDBNull(1) ? "" : Convert.ToString(reader.GetValue(1)) ?? "",
                    perNo = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ad = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    soyad = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    gun = reader.GetInt32(5),
                    izinTipi,
                    aciklama = reader.IsDBNull(7) ? "" : reader.GetString(7)
                });
            }
        }
        catch (Exception ex)
        {
            return Json(new { hata = ex.Message });
        }

        return Json(izinler);
    }

    // ================================================================
    // ✅ İzin kaydet (tek veya toplu) - DÜZELTİLDİ
    // ================================================================
    [HttpPost]
    public async Task<IActionResult> KaydetIzin([FromBody] List<PuantajIzinModel> izinler)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        if (izinler == null || izinler.Count == 0)
            return Json(new { basarili = false, mesaj = "Kaydedilecek veri yok" });
        if (izinler.Any(izin => izin is null || string.IsNullOrWhiteSpace(izin.PersonelId)))
            return Json(new { basarili = false, mesaj = "Geçersiz personel kimliği." });

        if (izinler.Any(izin =>
                izin.Yil < 1 ||
                izin.Ay is < 1 or > 12 ||
                izin.Gun < 1 ||
                izin.Gun > DateTime.DaysInMonth(izin.Yil, izin.Ay)))
            return Json(new { basarili = false, mesaj = "Geçersiz yıl, ay veya gün." });

        int eklenen = 0, silinen = 0;
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var donem in izinler.Select(izin => (izin.Yil, izin.Ay)).Distinct())
            {
                if (await AyKilitKontrol.KilitliMiAsync(connection, donem.Yil, donem.Ay))
                    return Json(new { basarili = false, mesaj = $"{donem.Yil} yılı {donem.Ay}. ay kilitlenmiştir. Kayıt veya değişiklik yapılamaz." });
            }

            foreach (var izin in izinler)
            {
                izin.IzinTipi = NormalizePuantajDurumu(izin.IzinTipi);
                // İzin tipi X ise (çalıştı) kaydı sil — sadece izin günleri DB'de tutulur
                if (izin.IzinTipi == "X" || string.IsNullOrEmpty(izin.IzinTipi))
                {
                    await using var delCmd = new MySqlCommand(
                        "DELETE FROM puantaj_izin WHERE personel_id = @pid AND yil = @yil AND ay = @ay AND gun = @gun",
                        connection);
                    delCmd.Parameters.AddWithValue("@pid", izin.PersonelId);
                    delCmd.Parameters.AddWithValue("@yil", izin.Yil);
                    delCmd.Parameters.AddWithValue("@ay", izin.Ay);
                    delCmd.Parameters.AddWithValue("@gun", izin.Gun);
                    var deleted = await delCmd.ExecuteNonQueryAsync();
                    if (deleted > 0) silinen++;
                }
                else
                {
                    // UPSERT: varsa güncelle, yoksa ekle
                    await using var upsertCmd = new MySqlCommand(
                        @"INSERT INTO puantaj_izin (personel_id, yil, ay, gun, izin_tipi, aciklama)
                          VALUES (@pid, @yil, @ay, @gun, @tip, @aciklama)
                          ON DUPLICATE KEY UPDATE izin_tipi = @tip, aciklama = @aciklama",
                        connection);
                    upsertCmd.Parameters.AddWithValue("@pid", izin.PersonelId);
                    upsertCmd.Parameters.AddWithValue("@yil", izin.Yil);
                    upsertCmd.Parameters.AddWithValue("@ay", izin.Ay);
                    upsertCmd.Parameters.AddWithValue("@gun", izin.Gun);
                    upsertCmd.Parameters.AddWithValue("@tip", izin.IzinTipi);
                    upsertCmd.Parameters.AddWithValue("@aciklama", izin.Aciklama ?? "");
                    await upsertCmd.ExecuteNonQueryAsync();
                    eklenen++;
                }
            }
        }
        catch (Exception ex)
        {
            return Json(new { basarili = false, mesaj = ex.Message });
        }

        return Json(new { basarili = true, mesaj = $"{eklenen} izin kaydedildi, {silinen} kayıt silindi" });
    }

    // Resmi tatilleri getir (yıl ve ay bazlı)
    [HttpGet]
    public async Task<IActionResult> GetResmiTatiller(int yil, int ay)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();

        var tatiller = new List<object>();
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var cmd = new MySqlCommand(
                "SELECT DAY(tatil_tarihi) AS gun, tatil_adi FROM kktc_resmi_tatiller WHERE YEAR(tatil_tarihi) = @yil AND MONTH(tatil_tarihi) = @ay ORDER BY tatil_tarihi",
                connection);
            cmd.Parameters.AddWithValue("@yil", yil);
            cmd.Parameters.AddWithValue("@ay", ay);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tatiller.Add(new
                {
                    gun = reader.GetInt32(0),
                    tatilAdi = reader.GetString(1)
                });
            }
        }
        catch (Exception ex)
        {
            return Json(new { hata = ex.Message });
        }

        return Json(tatiller);
    }

    // ---- FastReport A4 Rapor Standardı (v3) ----
    // Önizleme ve PDF aynı .frx şablonundan üretilir: Reports/Puantaj/Puantaj.Yatay.frx
    // Excel çıktısı kaldırıldı (standart Bölüm 12: Excel .frx'ten üretilmez).

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Rapor(int yil, int ay, string? birim, string? statu, string? arama, string yon = "yatay")
    {
        if (!RaporYon.TryCoz(yon, out var y))
            return BadRequest("Geçersiz yön. Geçerli değerler: dikey | yatay");
        if (!PuantajRaporTanimi.YonDestekliyor(y))
            return BadRequest("Puantaj raporu için yalnızca 'yatay' şablonu mevcuttur.");

        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return RedirectToAction("Login", "Account");
        if (yil < 2000 || yil > 2100 || ay < 1 || ay > 12)
            return BadRequest("Geçersiz puantaj dönemi.");

        var (tablo, genel) = await GetPuantajRaporTablosuAsync(yil, ay, birim, statu, arama);
        var (ds, prm) = PuantajRaporVerisiHazirla(tablo, yil, ay, birim, genel);

        var web = _raporServisi.Onizleme(PuantajRaporTanimi, y, ds, prm);
        ViewBag.Yon = y;
        ViewBag.DesteklenenYonler = PuantajRaporTanimi.Yonler();
        ViewBag.RaporQuery = $"yil={yil}&ay={ay}&birim={Uri.EscapeDataString(birim ?? "all")}"
            + $"&statu={Uri.EscapeDataString(statu ?? "__TUMU__")}&arama={Uri.EscapeDataString(arama ?? "")}";
        ViewBag.PdfUrl = Url.Action(nameof(RaporPdf), new { yil, ay, birim, statu, arama, yon = y.ToString().ToLowerInvariant(), indir = true });
        return View("~/Views/Shared/Rapor.cshtml", web);
    }

    // PDF İNDİR / TARAYICIDA AÇ — aynı .frx şablonundan üretilir (A4 yatay).
    [HttpGet]
    public async Task<IActionResult> RaporPdf(int yil, int ay, string? birim, string? statu, string? arama, string yon = "yatay", bool indir = false)
    {
        if (!RaporYon.TryCoz(yon, out var y))
            return BadRequest("Geçersiz yön. Geçerli değerler: dikey | yatay");
        if (!PuantajRaporTanimi.YonDestekliyor(y))
            return BadRequest("Puantaj raporu için yalnızca 'yatay' şablonu mevcuttur.");

        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return Unauthorized();
        if (yil < 2000 || yil > 2100 || ay < 1 || ay > 12)
            return BadRequest("Geçersiz puantaj dönemi.");

        var (tablo, genel) = await GetPuantajRaporTablosuAsync(yil, ay, birim, statu, arama);
        var (ds, prm) = PuantajRaporVerisiHazirla(tablo, yil, ay, birim, genel);
        var cikti = _raporServisi.Pdf(PuantajRaporTanimi, y, ds, prm);

        // Hassas rapor yanıtı istemci/proxy önbelleğine alınmaz.
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";

        return indir ? File(cikti.Icerik, "application/pdf", $"puantaj-{yil}-{ay:D2}.pdf")
                     : File(cikti.Icerik, "application/pdf");
    }

    // Rapor verisi: personel + izin işaretleri + satır/genel toplamlar.
    // Yetki kuralı: admin olmayan kullanıcının birimi zorunlu olarak kullanılır.
    private async Task<(DataTable Tablo, (int X, int I, int O) Genel)> GetPuantajRaporTablosuAsync(
        int yil, int ay, string? birim, string? statu, string? arama)
    {
        var rol = (HttpContext.Session.GetString("Rol") ?? "birim_amiri").Trim().ToLowerInvariant();
        var kullaniciBirim = HttpContext.Session.GetString("Birim") ?? "";
        if (rol != "admin")
            birim = kullaniciBirim;
        else if (string.IsNullOrWhiteSpace(birim))
            birim = "all";

        var aramaMetni = (arama ?? "").Trim();
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        var compareInfo = culture.CompareInfo;
        var personeller = new List<(string Id, string AdSoyad, string Birim, string Statu)>();
        var izinler = new Dictionary<(string PersonelId, int Gun), string>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var tumBirimler = rol == "admin" && birim == "all";
        var personelSql = tumBirimler
            ? "SELECT per_no, ad, soyad, birim_adi, per_statu FROM personeller ORDER BY birim_adi, ad, soyad"
            : "SELECT per_no, ad, soyad, birim_adi, per_statu FROM personeller WHERE birim_adi = @birim ORDER BY ad, soyad";
        await using (var personelCmd = new MySqlCommand(personelSql, connection))
        {
            if (!tumBirimler)
                personelCmd.Parameters.AddWithValue("@birim", birim ?? "");

            await using var reader = await personelCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "";
                var adSoyad = (reader.IsDBNull(1) ? "" : reader.GetString(1)) + " " +
                              (reader.IsDBNull(2) ? "" : reader.GetString(2));
                var personelBirim = reader.IsDBNull(3) ? "" : reader.GetString(3);
                var personelStatu = reader.IsDBNull(4) ? "" : reader.GetString(4).Trim();

                if (statu != null && statu != "__TUMU__" && personelStatu != statu)
                    continue;
                if (!string.IsNullOrEmpty(aramaMetni) &&
                    compareInfo.IndexOf(adSoyad, aramaMetni, CompareOptions.IgnoreCase) < 0)
                    continue;

                personeller.Add((id, adSoyad.Trim(), personelBirim, personelStatu));
            }
        }

        await using (var izinCmd = new MySqlCommand(
            "SELECT personel_id, gun, izin_tipi FROM puantaj_izin WHERE yil = @yil AND ay = @ay", connection))
        {
            izinCmd.Parameters.AddWithValue("@yil", yil);
            izinCmd.Parameters.AddWithValue("@ay", ay);
            await using var reader = await izinCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var personelId = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "";
                var gun = reader.GetInt32(1);
                var izinTipi = NormalizePuantajDurumu(reader.IsDBNull(2) ? null : reader.GetString(2));
                if (gun is >= 1 and <= 30)
                    izinler[(personelId, gun)] = izinTipi;
            }
        }

        var table = new DataTable("Puantaj") { Locale = culture };
        table.Columns.Add("AdSoyad", typeof(string));
        table.Columns.Add("Birim", typeof(string));
        for (var gun = 1; gun <= 30; gun++)
            table.Columns.Add($"Gun{gun:D2}", typeof(string));
        table.Columns.Add("XSayisi", typeof(int));
        table.Columns.Add("ISayisi", typeof(int));
        table.Columns.Add("OSayisi", typeof(int));
        table.Columns.Add("Hakkedis", typeof(int));

        var izinSayimTurleri = new HashSet<string>(new[] { "İ", "Gİ", "ÇR", "Vİ", "Hİ", "R", "Sİ", "PT" }, StringComparer.Ordinal);
        var genelX = 0;
        var genelI = 0;
        var genelO = 0;
        foreach (var personel in personeller)
        {
            var row = table.NewRow();
            row["AdSoyad"] = personel.AdSoyad;
            row["Birim"] = personel.Birim;
            var xSayisi = 0;
            var iSayisi = 0;
            var oSayisi = 0;
            var odeneksizIzinSayisi = 0;
            var partTimeSayisi = 0;

            for (var gun = 1; gun <= 30; gun++)
            {
                var durum = izinler.TryGetValue((personel.Id, gun), out var izinTipi) ? izinTipi : "X";
                row[$"Gun{gun:D2}"] = durum;
                if (durum == "X") xSayisi++;
                if (izinSayimTurleri.Contains(durum)) iSayisi++;
                if (durum is "Öİ" or "ÖDİ")
                {
                    oSayisi++;
                    odeneksizIzinSayisi++;
                }
                if (durum == "PT") partTimeSayisi++;
            }

            var hakkedis = 30 - odeneksizIzinSayisi - partTimeSayisi;
            row["XSayisi"] = xSayisi;
            row["ISayisi"] = iSayisi;
            row["OSayisi"] = oSayisi;
            row["Hakkedis"] = hakkedis;
            table.Rows.Add(row);
            genelX += xSayisi;
            genelI += iSayisi;
            genelO += oSayisi;
        }

        return (table, (genelX, genelI, genelO));
    }

    private static string NormalizePuantajDurumu(string? durum) => durum switch
    {
        null or "" => "X",
        "T" => "X",
        "ÇH" => "ÇR",
        "H" => "R",
        "ÖDİ" => "Öİ",
        _ => durum
    };

    /// Tablo ve kolon adları .frx ile birebir eşleşir: DataTable "Puantaj" → ReferenceName "Data.Puantaj".
    private (DataSet, Dictionary<string, object?>) PuantajRaporVerisiHazirla(
        DataTable tablo, int yil, int ay, string? birim, (int X, int I, int O) genel)
    {
        var rol = (HttpContext.Session.GetString("Rol") ?? "birim_amiri").Trim().ToLowerInvariant();
        var culture = CultureInfo.GetCultureInfo("tr-TR");

        var ds = new DataSet("Data");
        ds.Tables.Add(tablo);

        var prm = new Dictionary<string, object?>
        {
            ["BaslikAdi"]     = "PUANTAJ TAKVİMİ",
            ["OlusturanAd"]   = HttpContext.Session.GetString("KullaniciAdi") ?? "GAMABEL YATIRIM LTD",
            ["Tarih"]         = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            ["Donem"]         = $"{culture.DateTimeFormat.GetMonthName(ay)} {yil}",
            ["Birim"]         = rol == "admin" && birim == "all" ? "Tüm Birimler" : birim ?? "",
            ["PersonelSayisi"] = tablo.Rows.Count,
            ["GenelXSayisi"]  = genel.X,
            ["GenelISayisi"]  = genel.I,
            ["GenelOSayisi"]  = genel.O,
            ["Filigran"]      = null, // Yalnızca taslak/onay durumu güvenilir şekilde belirlenebiliyorsa doldurun.
            ["ImzaBirimSorumlusu"]   = "",
        };
        return (ds, prm);
    }
}