using Microsoft.AspNetCore.Mvc;
using gamabelmvc.Models;
using gamabelmvc.Models.STS;
using gamabelmvc.Services;
using MySqlConnector;
using OfficeOpenXml;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace gamabelmvc.Controllers;

public class AdminController : Controller
{
    private readonly DbConnectionFactory _dbFactory;
    private const string UrunTopluYuklePlanSessionKey = "Admin.UrunTopluYuklePlan";

    public AdminController(DbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // Yetki kontrolü
    private bool IsAdmin()
    {
        var rol = HttpContext.Session.GetString("Rol") ?? "";
        return rol == "Admin";
    }

    // ========== VERİTABANI YEDEKLEME / GERİ YÜKLEME ==========

    // GET: Admin/VeritabaniYedekleme
    public async Task<IActionResult> VeritabaniYedekleme()
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                var tables = await GetAllTableNamesAsync(conn);
                return View("~/Views/STS/Admin/VeritabaniYedekleme.cshtml", tables);
            }
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Tablolar listelenirken hata oluştu: {ex.Message}";
            return View("~/Views/STS/Admin/VeritabaniYedekleme.cshtml", new List<string>());
        }
    }

    // POST: Admin/YedekleSeciliTablolar
    [HttpPost]
    public async Task<IActionResult> YedekleSeciliTablolar(List<string>? selectedTables)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        if (selectedTables == null || selectedTables.Count == 0)
        {
            TempData["Hata"] = "Yedekleme için en az bir tablo seçmelisiniz.";
            return RedirectToAction("VeritabaniYedekleme");
        }

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                var allTables = await GetAllTableNamesAsync(conn);
                var selectedSafeTables = selectedTables
                    .Where(IsSafeTableName)
                    .Where(t => allTables.Contains(t, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (selectedSafeTables.Count == 0)
                {
                    TempData["Hata"] = "Geçerli tablo seçimi bulunamadı.";
                    return RedirectToAction("VeritabaniYedekleme");
                }

                var sb = new StringBuilder();
                sb.AppendLine("-- GAMABEL STS Seçili Tablolar Yedeği");
                sb.AppendLine($"-- Oluşturma Tarihi: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"-- Tablolar: {string.Join(", ", selectedSafeTables)}");
                sb.AppendLine("SET FOREIGN_KEY_CHECKS=0;");
                sb.AppendLine();

                foreach (var tableName in selectedSafeTables)
                {
                    await AppendCreateTableScriptAsync(conn, tableName, sb);
                    await AppendTableDataScriptAsync(conn, tableName, sb);
                }

                sb.AppendLine("SET FOREIGN_KEY_CHECKS=1;");

                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                var fileName = $"gamabel_selected_tables_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
                return File(bytes, "application/sql", fileName);
            }
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Yedekleme sırasında hata oluştu: {ex.Message}";
            return RedirectToAction("VeritabaniYedekleme");
        }
    }

    // POST: Admin/GeriYukleSeciliTablolar
    [HttpPost]
    public async Task<IActionResult> GeriYukleSeciliTablolar(IFormFile? sqlFile, List<string>? selectedTables, bool clearSelectedTables = true)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        if (sqlFile == null || sqlFile.Length == 0)
        {
            TempData["Hata"] = "Geri yükleme için bir .sql dosyası seçmelisiniz.";
            return RedirectToAction("VeritabaniYedekleme");
        }

        var extension = Path.GetExtension(sqlFile.FileName).ToLowerInvariant();
        if (extension != ".sql")
        {
            TempData["Hata"] = "Sadece .sql uzantılı dosyalar kabul edilir.";
            return RedirectToAction("VeritabaniYedekleme");
        }

        if (selectedTables == null || selectedTables.Count == 0)
        {
            TempData["Hata"] = "Geri yükleme için en az bir tablo seçmelisiniz.";
            return RedirectToAction("VeritabaniYedekleme");
        }

        if (sqlFile.Length > 20 * 1024 * 1024)
        {
            TempData["Hata"] = "SQL dosyası 20 MB'dan büyük olamaz.";
            return RedirectToAction("VeritabaniYedekleme");
        }

        try
        {
            string sqlContent;
            using (var reader = new StreamReader(sqlFile.OpenReadStream(), Encoding.UTF8, true))
            {
                sqlContent = await reader.ReadToEndAsync();
            }

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                var allTables = await GetAllTableNamesAsync(conn);
                var selectedSafeTables = selectedTables
                    .Where(IsSafeTableName)
                    .Where(t => allTables.Contains(t, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (selectedSafeTables.Count == 0)
                {
                    TempData["Hata"] = "Geçerli tablo seçimi bulunamadı.";
                    return RedirectToAction("VeritabaniYedekleme");
                }

                using var tx = await conn.BeginTransactionAsync();
                try
                {
                    using (var fkCmd = new MySqlCommand("SET FOREIGN_KEY_CHECKS=0;", conn, tx))
                    {
                        await fkCmd.ExecuteNonQueryAsync();
                    }

                    if (clearSelectedTables)
                    {
                        foreach (var table in selectedSafeTables)
                        {
                            using var truncateCmd = new MySqlCommand($"TRUNCATE TABLE {QuoteIdentifier(table)};", conn, tx);
                            await truncateCmd.ExecuteNonQueryAsync();
                        }
                    }

                    var statements = SplitSqlStatements(sqlContent);
                    var executedCount = 0;

                    foreach (var statement in statements)
                    {
                        var targetTable = TryExtractTargetTable(statement);
                        if (!string.IsNullOrWhiteSpace(targetTable) && !selectedSafeTables.Contains(targetTable))
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(targetTable) && !IsSessionLevelStatement(statement))
                        {
                            continue;
                        }

                        using var cmd = new MySqlCommand(statement, conn, tx);
                        await cmd.ExecuteNonQueryAsync();
                        executedCount++;
                    }

                    using (var fkCmd = new MySqlCommand("SET FOREIGN_KEY_CHECKS=1;", conn, tx))
                    {
                        await fkCmd.ExecuteNonQueryAsync();
                    }

                    await tx.CommitAsync();
                    TempData["Basari"] = $"Geri yükleme tamamlandı. Çalıştırılan SQL komutu: {executedCount}";
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            }

            return RedirectToAction("VeritabaniYedekleme");
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Geri yükleme sırasında hata oluştu: {ex.Message}";
            return RedirectToAction("VeritabaniYedekleme");
        }
    }

    // ========== ŞUBELERİ YÖNETİMİ ==========

    // GET: Admin/Subeler
    public async Task<IActionResult> Subeler()
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        try
        {
            var subeler = new List<StsSube>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureSubeResponsibleColumnAsync(conn);

                var query = @"SELECT s.*, k.AdSoyad as SorumluAdi
                              FROM stk_Sube s
                              LEFT JOIN stk_Kullanici k ON s.SorumluKullaniciId = k.Id
                              ORDER BY s.Ad";
                using (var cmd = new MySqlCommand(query, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            subeler.Add(new StsSube
                            {
                                Id = reader.GetInt32("Id"),
                                Ad = reader.GetString("Ad"),
                                Kod = reader.GetString("Kod"),
                                Tip = reader.GetString("Tip"),
                                AktifMi = reader.GetBoolean("AktifMi"),
                                SorumluKullaniciId = reader["SorumluKullaniciId"] == DBNull.Value ? null : Convert.ToInt32(reader["SorumluKullaniciId"]),
                                SorumluAdi = reader["SorumluAdi"] == DBNull.Value ? null : reader["SorumluAdi"]?.ToString()
                            });
                        }
                    }
                }

                var kullanicilar = new List<dynamic>();
                using (var userCmd = new MySqlCommand("SELECT Id, AdSoyad FROM stk_Kullanici WHERE AktifMi = true ORDER BY AdSoyad", conn))
                {
                    using (var reader = await userCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            kullanicilar.Add(new { Id = reader.GetInt32("Id"), AdSoyad = reader.GetString("AdSoyad") });
                        }
                    }
                }
                ViewBag.Kullanicilar = kullanicilar;
            }

            return View("~/Views/STS/Admin/Subeler.cshtml", subeler);
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<IActionResult> SubeSorumluAta(int subeId, int? sorumluKullaniciId)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureSubeResponsibleColumnAsync(conn);
                using (var cmd = new MySqlCommand("UPDATE stk_Sube SET SorumluKullaniciId = @SorumluKullaniciId WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", subeId);
                    cmd.Parameters.AddWithValue("@SorumluKullaniciId", sorumluKullaniciId.HasValue ? sorumluKullaniciId.Value : DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            TempData["Basari"] = "Şube sorumlusu güncellendi.";
            return RedirectToAction("Subeler");
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Şube sorumlusu atanamadı: {ex.Message}";
            return RedirectToAction("Subeler");
        }
    }

    // GET: Admin/SubeEkle
    public IActionResult SubeEkle()
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");
        return View("~/Views/STS/Admin/SubeEkle.cshtml", new StsSube());
    }

    // POST: Admin/SubeEkle
    [HttpPost]
    public async Task<IActionResult> SubeEkle(StsSube sube)
    {
        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                var query = @"
                    INSERT INTO stk_Sube (Ad, Kod, Tip, AktifMi)
                    VALUES (@Ad, @Kod, @Tip, @AktifMi)";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Ad", sube.Ad);
                    cmd.Parameters.AddWithValue("@Kod", sube.Kod);
                    cmd.Parameters.AddWithValue("@Tip", sube.Tip);
                    cmd.Parameters.AddWithValue("@AktifMi", sube.AktifMi);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return RedirectToAction("Subeler");
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // ========== ÜRÜN YÖNETİMİ ==========

    // GET: Admin/Urunler
    public async Task<IActionResult> Urunler()
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        try
        {
            var urunler = new List<StsUrun>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureUrunKodColumnAsync(conn);
                await EnsureGrupColumnAsync(conn);

                var query = "SELECT Id, Kod, Ad, Barkod, Birim, Firma, Grup, AktifMi FROM stk_Urun ORDER BY Ad";
                using (var cmd = new MySqlCommand(query, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            urunler.Add(new StsUrun
                            {
                                Id = reader.GetInt32("Id"),
                                Kod = reader["Kod"] == DBNull.Value ? null : reader["Kod"]?.ToString(),
                                Ad = reader.GetString("Ad"),
                                Barkod = reader["Barkod"] == DBNull.Value ? null : reader["Barkod"]?.ToString(),
                                Birim = reader.GetString("Birim"),
                                Firma = reader["Firma"] == DBNull.Value ? null : reader["Firma"]?.ToString(),
                                Grup = reader["Grup"] == DBNull.Value ? null : reader["Grup"]?.ToString(),
                                AktifMi = reader.GetBoolean("AktifMi")
                            });
                        }
                    }
                }
            }

            return View("~/Views/STS/Admin/Urunler.cshtml", urunler);
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // GET: Admin/UrunEkle
    public IActionResult UrunEkle()
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");
        return View("~/Views/STS/Admin/UrunEkle.cshtml", new StsUrun());
    }

    // POST: Admin/UrunEkle
    [HttpPost]
    public async Task<IActionResult> UrunEkle(StsUrun urun)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(urun.Kod) || string.IsNullOrWhiteSpace(urun.Ad))
        {
            TempData["Hata"] = "Kod ve ürün adı zorunludur.";
            return RedirectToAction("UrunEkle");
        }

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureUrunKodColumnAsync(conn);

                var duplicateQuery = "SELECT COUNT(*) FROM stk_Urun WHERE Kod = @Kod";
                using (var duplicateCmd = new MySqlCommand(duplicateQuery, conn))
                {
                    duplicateCmd.Parameters.AddWithValue("@Kod", urun.Kod.Trim());
                    var exists = Convert.ToInt32(await duplicateCmd.ExecuteScalarAsync());
                    if (exists > 0)
                    {
                        TempData["Hata"] = "Bu ürün kodu zaten kayıtlı.";
                        return RedirectToAction("UrunEkle");
                    }
                }

                var query = @"
                    INSERT INTO stk_Urun (Kod, Ad, Barkod, Birim, AktifMi)
                    VALUES (@Kod, @Ad, @Barkod, @Birim, @AktifMi)";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Kod", urun.Kod.Trim());
                    cmd.Parameters.AddWithValue("@Ad", urun.Ad);
                    cmd.Parameters.AddWithValue("@Barkod", urun.Barkod ?? "");
                    cmd.Parameters.AddWithValue("@Birim", urun.Birim);
                    cmd.Parameters.AddWithValue("@AktifMi", urun.AktifMi);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            TempData["Basari"] = "Ürün eklendi.";
            return RedirectToAction("Urunler");
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Ürün eklenemedi: {ex.Message}";
            return RedirectToAction("UrunEkle");
        }
    }

    // GET: Admin/UrunTopluYukle
    public async Task<IActionResult> UrunTopluYukle()
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        try
        {
            using var conn = await _dbFactory.CreateConnectionAsync();
            await EnsureUrunTopluYukleLogTableAsync(conn);

            var model = new UrunTopluYukleResult
            {
                RecentLogs = await GetRecentUrunTopluYukleLogsAsync(conn)
            };

            return View("~/Views/STS/Admin/UrunTopluYukle.cshtml", model);
        }
        catch
        {
            return View("~/Views/STS/Admin/UrunTopluYukle.cshtml", new UrunTopluYukleResult());
        }
    }

    // POST: Admin/UrunTopluYukle
    [HttpPost]
    public async Task<IActionResult> UrunTopluYukle(IFormFile file, string submitAction = "preview")
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        if (file == null || file.Length == 0)
        {
            TempData["Hata"] = "Lütfen bir Excel dosyası seçiniz.";
            return RedirectToAction("UrunTopluYukle");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".xlsx" && extension != ".xls")
        {
            TempData["Hata"] = "Sadece Excel dosyaları (.xlsx, .xls) yüklenebilir.";
            return RedirectToAction("UrunTopluYukle");
        }

        try
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureUrunKodColumnAsync(conn);
                await EnsureGrupColumnAsync(conn);
                await EnsureUrunTopluYukleLogTableAsync(conn);

                var plan = await BuildUrunTopluYuklePlanAsync(conn, file);
                SaveUrunTopluYuklePlan(plan);

                var result = MapPlanToResult(plan, isPreview: true);
                result.RecentLogs = await GetRecentUrunTopluYukleLogsAsync(conn);

                await InsertUrunTopluYukleLogAsync(conn, "Preview", HttpContext.Session.GetString("KullaniciAdi") ?? "Admin", result);
                return View("~/Views/STS/Admin/UrunTopluYukle.cshtml", result);
            }
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Toplu yükleme hatası: {ex.Message}";
            return RedirectToAction("UrunTopluYukle");
        }
    }

    [HttpPost]
    public async Task<IActionResult> UrunTopluYukleUygula(string previewToken, bool deleteMissingProducts = false)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        var plan = LoadUrunTopluYuklePlan();
        if (plan == null || string.IsNullOrWhiteSpace(previewToken) || !string.Equals(plan.Token, previewToken, StringComparison.Ordinal))
        {
            TempData["Hata"] = "Ön izleme verisi bulunamadı veya süresi doldu. Lütfen dosyayı yeniden yükleyin.";
            return RedirectToAction("UrunTopluYukle");
        }

        try
        {
            using var conn = await _dbFactory.CreateConnectionAsync();
            await EnsureUrunKodColumnAsync(conn);
            await EnsureGrupColumnAsync(conn);
            await EnsureUrunTopluYukleLogTableAsync(conn);

            using var tx = await conn.BeginTransactionAsync();

            if (deleteMissingProducts)
            {
                if (plan.KeepCodes.Count > 0)
                {
                    var parameterNames = new List<string>();
                    using var deleteCmd = new MySqlCommand();
                    deleteCmd.Connection = conn;
                    deleteCmd.Transaction = tx;

                    for (var i = 0; i < plan.KeepCodes.Count; i++)
                    {
                        var parameterName = $"@KeepCode{i}";
                        parameterNames.Add(parameterName);
                        deleteCmd.Parameters.AddWithValue(parameterName, plan.KeepCodes[i]);
                    }

                    deleteCmd.CommandText = $@"
                        DELETE FROM stk_Urun
                        WHERE Kod IS NOT NULL
                          AND Kod <> ''
                          AND Kod NOT IN ({string.Join(", ", parameterNames)})";
                    await deleteCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    using var deleteAllCmd = new MySqlCommand(@"
                        DELETE FROM stk_Urun
                        WHERE Kod IS NOT NULL
                          AND Kod <> ''", conn, tx);
                    await deleteAllCmd.ExecuteNonQueryAsync();
                }
            }

            foreach (var item in plan.Items)
            {
                if (item.IslemTuru == "Update" && item.UrunId.HasValue)
                {
                    using var updateCmd = new MySqlCommand(@"
                        UPDATE stk_Urun
                        SET Ad = @Ad,
                            Birim = @Birim,
                            Grup = @Grup,
                            Barkod = CASE WHEN (Barkod IS NULL OR Barkod = '') AND @Barkod IS NOT NULL AND @Barkod != '' THEN @Barkod ELSE Barkod END
                        WHERE Id = @Id", conn, tx);
                    updateCmd.Parameters.AddWithValue("@Id", item.UrunId.Value);
                    updateCmd.Parameters.AddWithValue("@Ad", item.Ad);
                    updateCmd.Parameters.AddWithValue("@Birim", item.Birim);
                    updateCmd.Parameters.AddWithValue("@Grup", string.IsNullOrWhiteSpace(item.Grup) ? (object)DBNull.Value : item.Grup);
                    updateCmd.Parameters.AddWithValue("@Barkod", string.IsNullOrWhiteSpace(item.Barkod) ? (object)DBNull.Value : item.Barkod);
                    await updateCmd.ExecuteNonQueryAsync();
                }
                else if (item.IslemTuru == "Insert")
                {
                    using var insertCmd = new MySqlCommand(@"
                        INSERT INTO stk_Urun (Kod, Ad, Barkod, Birim, Grup, AktifMi)
                        VALUES (@Kod, @Ad, @Barkod, @Birim, @Grup, true)", conn, tx);
                    insertCmd.Parameters.AddWithValue("@Kod", item.Kod);
                    insertCmd.Parameters.AddWithValue("@Ad", item.Ad);
                    insertCmd.Parameters.AddWithValue("@Barkod", string.IsNullOrWhiteSpace(item.Barkod) ? (object)DBNull.Value : item.Barkod);
                    insertCmd.Parameters.AddWithValue("@Birim", item.Birim);
                    insertCmd.Parameters.AddWithValue("@Grup", string.IsNullOrWhiteSpace(item.Grup) ? (object)DBNull.Value : item.Grup);
                    await insertCmd.ExecuteNonQueryAsync();
                }
            }

            await tx.CommitAsync();

            var result = MapPlanToResult(plan, isPreview: false);
            result.DeleteMissingProducts = deleteMissingProducts;
            result.RecentLogs = await GetRecentUrunTopluYukleLogsAsync(conn);
            await InsertUrunTopluYukleLogAsync(conn, "Apply", HttpContext.Session.GetString("KullaniciAdi") ?? "Admin", result);
            ClearUrunTopluYuklePlan();
            TempData["Basari"] = deleteMissingProducts
                ? "Ön izleme onaylandı, toplu yükleme uygulandı ve Excel'de olmayan ürünler veritabanından silindi."
                : "Ön izleme onaylandı ve toplu yükleme uygulandı.";
            return View("~/Views/STS/Admin/UrunTopluYukle.cshtml", result);
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Toplu yükleme uygulama hatası: {ex.Message}";
            return RedirectToAction("UrunTopluYukle");
        }
    }

    // ========== KULLANICI YÖNETİMİ ==========

    // GET: Admin/Kullanicilar
    public async Task<IActionResult> Kullanicilar()
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        try
        {
            var kullanicilar = new List<dynamic>();

            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                var query = @"
                    SELECT k.*, s.Ad as SubeAdi
                    FROM stk_Kullanici k
                    JOIN stk_Sube s ON k.SubeId = s.Id
                    ORDER BY k.AdSoyad";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            kullanicilar.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                AdSoyad = reader.GetString("AdSoyad"),
                                KullaniciAdi = reader.GetString("KullaniciAdi"),
                                SubeAdi = reader.GetString("SubeAdi"),
                                Rol = reader.GetString("Rol"),
                                AktifMi = reader.GetBoolean("AktifMi")
                            });
                        }
                    }
                }
            }

            return View("~/Views/STS/Admin/Kullanicilar.cshtml", kullanicilar);
        }
        catch (Exception ex)
        {
            return BadRequest($"Hata: {ex.Message}");
        }
    }

    // GET: Admin/KullaniciEkle
    public async Task<IActionResult> KullaniciEkle()
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        using (var conn = await _dbFactory.CreateConnectionAsync())
        {
            var subeler = new List<StsSube>();
            using (var cmd = new MySqlCommand("SELECT Id, Ad FROM stk_Sube WHERE AktifMi = true ORDER BY Ad", conn))
            {
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        subeler.Add(new StsSube { Id = reader.GetInt32("Id"), Ad = reader.GetString("Ad") });
                    }
                }
            }
            ViewBag.Subeler = subeler;
        }

        return View("~/Views/STS/Admin/KullaniciEkle.cshtml", new StsKullanici());
    }

    // POST: Admin/KullaniciEkle
    [HttpPost]
    public async Task<IActionResult> KullaniciEkle(StsKullanici kullanici, string sifre)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        if (string.IsNullOrWhiteSpace(kullanici.AdSoyad) || string.IsNullOrWhiteSpace(kullanici.KullaniciAdi) || string.IsNullOrWhiteSpace(sifre))
        {
            TempData["Hata"] = "Ad Soyad, Kullanıcı Adı ve Şifre zorunludur.";
            return RedirectToAction("KullaniciEkle");
        }

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                // Kullanıcı adı benzersiz kontrolü
                var checkQuery = "SELECT COUNT(*) FROM stk_Kullanici WHERE KullaniciAdi = @KullaniciAdi";
                using (var checkCmd = new MySqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@KullaniciAdi", kullanici.KullaniciAdi);
                    var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                    if (exists > 0)
                    {
                        TempData["Hata"] = "Bu kullanıcı adı zaten kayıtlı.";
                        return RedirectToAction("KullaniciEkle");
                    }
                }

                var sifreHash = BCrypt.Net.BCrypt.HashPassword(sifre);
                var query = @"
                    INSERT INTO stk_Kullanici (SubeId, AdSoyad, KullaniciAdi, SifreHash, Rol, AktifMi)
                    VALUES (@SubeId, @AdSoyad, @KullaniciAdi, @SifreHash, @Rol, @AktifMi)";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@SubeId", kullanici.SubeId);
                    cmd.Parameters.AddWithValue("@AdSoyad", kullanici.AdSoyad);
                    cmd.Parameters.AddWithValue("@KullaniciAdi", kullanici.KullaniciAdi);
                    cmd.Parameters.AddWithValue("@SifreHash", sifreHash);
                    cmd.Parameters.AddWithValue("@Rol", string.IsNullOrWhiteSpace(kullanici.Rol) ? "SubePersoneli" : kullanici.Rol);
                    cmd.Parameters.AddWithValue("@AktifMi", true);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            TempData["Basari"] = "Kullanıcı başarıyla eklendi.";
            return RedirectToAction("Kullanicilar");
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Kullanıcı ekleme hatası: {ex.Message}";
            return RedirectToAction("KullaniciEkle");
        }
    }

    // GET: Admin/KullaniciDuzenle/5
    public async Task<IActionResult> KullaniciDuzenle(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        using (var conn = await _dbFactory.CreateConnectionAsync())
        {
            var model = new StsKullanici();

            var query = "SELECT Id, SubeId, AdSoyad, KullaniciAdi, Rol, AktifMi FROM stk_Kullanici WHERE Id = @Id";
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                    {
                        TempData["Hata"] = "Kullanıcı bulunamadı.";
                        return RedirectToAction("Kullanicilar");
                    }

                    model.Id = reader.GetInt32("Id");
                    model.SubeId = reader.GetInt32("SubeId");
                    model.AdSoyad = reader.GetString("AdSoyad");
                    model.KullaniciAdi = reader.GetString("KullaniciAdi");
                    model.Rol = reader.GetString("Rol");
                    model.AktifMi = reader.GetBoolean("AktifMi");
                }
            }

            var subeler = new List<StsSube>();
            using (var cmd = new MySqlCommand("SELECT Id, Ad FROM stk_Sube WHERE AktifMi = true ORDER BY Ad", conn))
            {
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        subeler.Add(new StsSube { Id = reader.GetInt32("Id"), Ad = reader.GetString("Ad") });
                    }
                }
            }
            ViewBag.Subeler = subeler;

            return View("~/Views/STS/Admin/KullaniciDuzenle.cshtml", model);
        }
    }

    // POST: Admin/KullaniciDuzenle
    [HttpPost]
    public async Task<IActionResult> KullaniciDuzenle(StsKullanici kullanici, string? yeniSifre)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                var query = @"
                    UPDATE stk_Kullanici
                    SET SubeId = @SubeId,
                        AdSoyad = @AdSoyad,
                        Rol = @Rol,
                        AktifMi = @AktifMi
                    WHERE Id = @Id";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", kullanici.Id);
                    cmd.Parameters.AddWithValue("@SubeId", kullanici.SubeId);
                    cmd.Parameters.AddWithValue("@AdSoyad", kullanici.AdSoyad);
                    cmd.Parameters.AddWithValue("@Rol", kullanici.Rol);
                    cmd.Parameters.AddWithValue("@AktifMi", kullanici.AktifMi);
                    await cmd.ExecuteNonQueryAsync();
                }

                if (!string.IsNullOrWhiteSpace(yeniSifre))
                {
                    var sifreHash = BCrypt.Net.BCrypt.HashPassword(yeniSifre);
                    var sifreQuery = "UPDATE stk_Kullanici SET SifreHash = @SifreHash WHERE Id = @Id";
                    using (var cmd = new MySqlCommand(sifreQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", kullanici.Id);
                        cmd.Parameters.AddWithValue("@SifreHash", sifreHash);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            TempData["Basari"] = "Kullanıcı başarıyla güncellendi.";
            return RedirectToAction("Kullanicilar");
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Kullanıcı güncelleme hatası: {ex.Message}";
            return RedirectToAction("Kullanicilar");
        }
    }

    // POST: Admin/KullaniciSil/5
    [HttpPost]
    public async Task<IActionResult> KullaniciSil(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                var query = "DELETE FROM stk_Kullanici WHERE Id = @Id";
                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            TempData["Basari"] = "Kullanıcı silindi.";
            return RedirectToAction("Kullanicilar");
        }
        catch (Exception ex)
        {
            TempData["Hata"] = $"Kullanıcı silme hatası: {ex.Message}";
            return RedirectToAction("Kullanicilar");
        }
    }

    private async Task<UrunTopluYuklePlan> BuildUrunTopluYuklePlanAsync(MySqlConnection conn, IFormFile file)
    {
        var plan = new UrunTopluYuklePlan
        {
            Token = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            SourceFileName = file.FileName
        };

        using (var countCmd = new MySqlCommand("SELECT COUNT(*) FROM stk_Urun", conn))
        {
            plan.MysqlUrunSayisi = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        }

        var mevcutUrunler = new Dictionary<string, dynamic>(StringComparer.OrdinalIgnoreCase);
        var barkodSahipleri = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using (var mevcutCmd = new MySqlCommand("SELECT Id, Kod, Ad, Birim, Grup, Barkod FROM stk_Urun WHERE Kod IS NOT NULL AND Kod != ''", conn))
        using (var reader = await mevcutCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32("Id");
                var kod = reader["Kod"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(kod))
                    continue;

                var mevcutBarkod = reader["Barkod"] == DBNull.Value ? string.Empty : reader["Barkod"]?.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(mevcutBarkod) && !barkodSahipleri.ContainsKey(mevcutBarkod))
                {
                    barkodSahipleri[mevcutBarkod] = id;
                }

                mevcutUrunler[kod] = new
                {
                    Id = id,
                    Ad = reader["Ad"]?.ToString()?.Trim() ?? string.Empty,
                    Birim = reader["Birim"]?.ToString()?.Trim() ?? string.Empty,
                    Grup = reader["Grup"] == DBNull.Value ? string.Empty : reader["Grup"]?.ToString()?.Trim() ?? string.Empty,
                    Barkod = mevcutBarkod
                };
            }
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        using var package = new ExcelPackage(stream);
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        if (worksheet == null || worksheet.Dimension == null)
        {
            throw new InvalidOperationException("Excel dosyasında okunabilir sayfa bulunamadı.");
        }

        var duplicateKodlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var startRow = 2;
        var endRow = worksheet.Dimension.End.Row;

        for (var row = startRow; row <= endRow; row++)
        {
            var kod = worksheet.Cells[row, 2].Text?.Trim();
            var ad = worksheet.Cells[row, 3].Text?.Trim();
            var birim = worksheet.Cells[row, 5].Text?.Trim();
            var grup = worksheet.Cells[row, 24].Text?.Trim();
            var barkod = worksheet.Cells[row, 34].Text?.Trim();

            if (string.IsNullOrWhiteSpace(kod) && string.IsNullOrWhiteSpace(ad))
            {
                continue;
            }

                plan.ExcelSatirSayisi++;

            if (string.IsNullOrWhiteSpace(kod))
            {
                plan.HataliSatir++;
                plan.HataliSatirlar.Add(new HataliUrunSatiri
                {
                    SatirNumarasi = row,
                    Kod = string.Empty,
                    Ad = ad ?? string.Empty,
                    HataNedeni = "Kod (B sütunu) boş olamaz."
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(ad))
            {
                plan.HataliSatir++;
                plan.HataliSatirlar.Add(new HataliUrunSatiri
                {
                    SatirNumarasi = row,
                    Kod = kod,
                    Ad = string.Empty,
                    HataNedeni = "Ürün adı (C sütunu) boş olamaz."
                });
                continue;
            }

            if (!duplicateKodlar.Add(kod))
            {
                plan.Atlandi++;
                plan.UyariSatir++;
                plan.UyariSatirlar.Add(new UyariUrunSatiri
                {
                    SatirNumarasi = row,
                    Kod = kod,
                    Ad = ad,
                    UyariNedeni = "Excel içinde tekrar eden kod olduğu için satır atlandı."
                });
                continue;
            }

                plan.KeepCodes.Add(kod);

            var finalBirim = string.IsNullOrWhiteSpace(birim) ? "Adet" : birim;
            var finalGrup = grup ?? string.Empty;
            var finalBarkod = barkod ?? string.Empty;

            if (mevcutUrunler.TryGetValue(kod, out var mevcut))
            {
                if (!string.IsNullOrWhiteSpace(finalBarkod)
                    && barkodSahipleri.TryGetValue(finalBarkod, out var barkodSahibiId)
                    && barkodSahibiId != (int)mevcut.Id)
                {
                    plan.UyariSatir++;
                    plan.UyariSatirlar.Add(new UyariUrunSatiri
                    {
                        SatirNumarasi = row,
                        Kod = kod,
                        Ad = ad,
                        UyariNedeni = $"Barkod başka bir üründe kayıtlı olduğu için uygulanmayacak: {finalBarkod}"
                    });
                    finalBarkod = string.Empty;
                }

                var adAyni = string.Equals((string)mevcut.Ad, ad, StringComparison.OrdinalIgnoreCase);
                var birimAyni = string.Equals((string)mevcut.Birim, finalBirim, StringComparison.OrdinalIgnoreCase);
                var grupAyni = string.Equals((string)mevcut.Grup, finalGrup, StringComparison.OrdinalIgnoreCase);
                var barkodEklenecek = string.IsNullOrWhiteSpace((string)mevcut.Barkod) && !string.IsNullOrWhiteSpace(finalBarkod);

                if (adAyni && birimAyni && grupAyni && !barkodEklenecek)
                {
                    plan.Degismeyen++;
                    continue;
                }

                plan.Guncellenen++;
                plan.Items.Add(new UrunTopluYuklePlanItem
                {
                    SatirNumarasi = row,
                    IslemTuru = "Update",
                    UrunId = (int)mevcut.Id,
                    Kod = kod,
                    Ad = ad,
                    Birim = finalBirim,
                    Grup = finalGrup,
                    Barkod = finalBarkod,
                    MevcutAd = (string)mevcut.Ad,
                    MevcutBirim = (string)mevcut.Birim,
                    MevcutGrup = (string)mevcut.Grup,
                    MevcutBarkod = (string)mevcut.Barkod,
                    BarkodDegisecek = barkodEklenecek
                });

                if (!string.IsNullOrWhiteSpace(finalBarkod))
                {
                    barkodSahipleri[finalBarkod] = (int)mevcut.Id;
                }

                mevcutUrunler[kod] = new
                {
                    Id = (int)mevcut.Id,
                    Ad = ad,
                    Birim = finalBirim,
                    Grup = finalGrup,
                    Barkod = string.IsNullOrWhiteSpace(finalBarkod) ? (string)mevcut.Barkod : finalBarkod
                };
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(finalBarkod) && barkodSahipleri.ContainsKey(finalBarkod))
                {
                    plan.UyariSatir++;
                    plan.UyariSatirlar.Add(new UyariUrunSatiri
                    {
                        SatirNumarasi = row,
                        Kod = kod,
                        Ad = ad,
                        UyariNedeni = $"Barkod başka bir üründe kayıtlı olduğu için yeni kayıtta boş bırakılacak: {finalBarkod}"
                    });
                    finalBarkod = string.Empty;
                }

                plan.EklendiBayit++;
                plan.Items.Add(new UrunTopluYuklePlanItem
                {
                    SatirNumarasi = row,
                    IslemTuru = "Insert",
                    Kod = kod,
                    Ad = ad,
                    Birim = finalBirim,
                    Grup = finalGrup,
                    Barkod = finalBarkod
                });

                if (!string.IsNullOrWhiteSpace(finalBarkod))
                {
                    barkodSahipleri[finalBarkod] = -row;
                }

                mevcutUrunler[kod] = new
                {
                    Id = -row,
                    Ad = ad,
                    Birim = finalBirim,
                    Grup = finalGrup,
                    Barkod = finalBarkod
                };
            }
        }

        plan.KeepCodes = plan.KeepCodes
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (plan.KeepCodes.Count > 0)
        {
            using var deleteCountCmd = new MySqlCommand();
            deleteCountCmd.Connection = conn;

            var parameterNames = new List<string>();
            for (var i = 0; i < plan.KeepCodes.Count; i++)
            {
                var parameterName = $"@KeepCode{i}";
                parameterNames.Add(parameterName);
                deleteCountCmd.Parameters.AddWithValue(parameterName, plan.KeepCodes[i]);
            }

            deleteCountCmd.CommandText = $@"
                SELECT COUNT(*)
                FROM stk_Urun
                WHERE Kod IS NOT NULL
                  AND Kod <> ''
                  AND Kod NOT IN ({string.Join(", ", parameterNames)})";

            plan.SilinecekKayit = Convert.ToInt32(await deleteCountCmd.ExecuteScalarAsync());

            using var silinecekCmd = new MySqlCommand();
            silinecekCmd.Connection = conn;
            for (var i = 0; i < plan.KeepCodes.Count; i++)
            {
                silinecekCmd.Parameters.AddWithValue($"@KeepCode{i}", plan.KeepCodes[i]);
            }

            silinecekCmd.CommandText = $@"
                SELECT Id, Kod, Ad, Birim, Grup, Barkod
                FROM stk_Urun
                WHERE Kod IS NOT NULL
                  AND Kod <> ''
                  AND Kod NOT IN ({string.Join(", ", parameterNames)})
                ORDER BY Ad, Kod";

            using var silinecekReader = await silinecekCmd.ExecuteReaderAsync();
            while (await silinecekReader.ReadAsync())
            {
                plan.SilinecekUrunler.Add(new SilinecekUrunSatiri
                {
                    Id = silinecekReader.GetInt32("Id"),
                    Kod = silinecekReader["Kod"]?.ToString() ?? string.Empty,
                    Ad = silinecekReader["Ad"]?.ToString() ?? string.Empty,
                    Birim = silinecekReader["Birim"]?.ToString() ?? string.Empty,
                    Grup = silinecekReader["Grup"] == DBNull.Value ? string.Empty : silinecekReader["Grup"]?.ToString() ?? string.Empty,
                    Barkod = silinecekReader["Barkod"] == DBNull.Value ? string.Empty : silinecekReader["Barkod"]?.ToString() ?? string.Empty
                });
            }
        }
        else
        {
            using var deleteAllCountCmd = new MySqlCommand(@"
                SELECT COUNT(*)
                FROM stk_Urun
                WHERE Kod IS NOT NULL
                  AND Kod <> ''", conn);
            plan.SilinecekKayit = Convert.ToInt32(await deleteAllCountCmd.ExecuteScalarAsync());

            using var silinecekReader = await new MySqlCommand(@"
                SELECT Id, Kod, Ad, Birim, Grup, Barkod
                FROM stk_Urun
                WHERE Kod IS NOT NULL
                  AND Kod <> ''
                ORDER BY Ad, Kod", conn).ExecuteReaderAsync();
            while (await silinecekReader.ReadAsync())
            {
                plan.SilinecekUrunler.Add(new SilinecekUrunSatiri
                {
                    Id = silinecekReader.GetInt32("Id"),
                    Kod = silinecekReader["Kod"]?.ToString() ?? string.Empty,
                    Ad = silinecekReader["Ad"]?.ToString() ?? string.Empty,
                    Birim = silinecekReader["Birim"]?.ToString() ?? string.Empty,
                    Grup = silinecekReader["Grup"] == DBNull.Value ? string.Empty : silinecekReader["Grup"]?.ToString() ?? string.Empty,
                    Barkod = silinecekReader["Barkod"] == DBNull.Value ? string.Empty : silinecekReader["Barkod"]?.ToString() ?? string.Empty
                });
            }
        }

        return plan;
    }

    private UrunTopluYukleResult MapPlanToResult(UrunTopluYuklePlan plan, bool isPreview)
    {
        return new UrunTopluYukleResult
        {
            EklendiBayit = plan.EklendiBayit,
            Guncellenen = plan.Guncellenen,
            Degismeyen = plan.Degismeyen,
            Atlandi = plan.Atlandi,
            HataliSatir = plan.HataliSatir,
            UyariSatir = plan.UyariSatir,
            SilinecekKayit = plan.SilinecekKayit,
            ExcelSatirSayisi = plan.ExcelSatirSayisi,
            MysqlUrunSayisi = plan.MysqlUrunSayisi,
            HataliSatirlar = plan.HataliSatirlar,
            UyariSatirlar = plan.UyariSatirlar,
            SilinecekUrunler = plan.SilinecekUrunler,
            GuncellenecekUrunler = plan.Items
                .Where(i => string.Equals(i.IslemTuru, "Update", StringComparison.OrdinalIgnoreCase))
                .ToList(),
            IsPreview = isPreview,
            CanApply = isPreview && (plan.Items.Count > 0 || plan.SilinecekKayit > 0),
            PreviewToken = plan.Token,
            SourceFileName = plan.SourceFileName
        };
    }

    private void SaveUrunTopluYuklePlan(UrunTopluYuklePlan plan)
    {
        var json = JsonSerializer.Serialize(plan);
        HttpContext.Session.SetString(UrunTopluYuklePlanSessionKey, json);
    }

    private UrunTopluYuklePlan? LoadUrunTopluYuklePlan()
    {
        var json = HttpContext.Session.GetString(UrunTopluYuklePlanSessionKey);
        return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<UrunTopluYuklePlan>(json);
    }

    private void ClearUrunTopluYuklePlan()
    {
        HttpContext.Session.Remove(UrunTopluYuklePlanSessionKey);
    }

    private async Task EnsureUrunTopluYukleLogTableAsync(MySqlConnection conn)
    {
        var query = @"
            CREATE TABLE IF NOT EXISTS stk_UrunTopluYuklemeLog (
                Id INT PRIMARY KEY AUTO_INCREMENT,
                IslemTipi VARCHAR(30) NOT NULL,
                DosyaAdi VARCHAR(255) NOT NULL,
                KullaniciAdi VARCHAR(100) NOT NULL,
                ExcelSatirSayisi INT NOT NULL DEFAULT 0,
                MysqlUrunSayisi INT NOT NULL DEFAULT 0,
                EklendiBayit INT NOT NULL DEFAULT 0,
                Guncellenen INT NOT NULL DEFAULT 0,
                Degismeyen INT NOT NULL DEFAULT 0,
                Atlandi INT NOT NULL DEFAULT 0,
                HataliSatir INT NOT NULL DEFAULT 0,
                UyariSatir INT NOT NULL DEFAULT 0,
                OlusturmaTarihi DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX idx_olusturma (OlusturmaTarihi)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;";

        using var cmd = new MySqlCommand(query, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InsertUrunTopluYukleLogAsync(MySqlConnection conn, string islemTipi, string kullaniciAdi, UrunTopluYukleResult result)
    {
        using var cmd = new MySqlCommand(@"
            INSERT INTO stk_UrunTopluYuklemeLog
            (IslemTipi, DosyaAdi, KullaniciAdi, ExcelSatirSayisi, MysqlUrunSayisi, EklendiBayit, Guncellenen, Degismeyen, Atlandi, HataliSatir, UyariSatir)
            VALUES
            (@IslemTipi, @DosyaAdi, @KullaniciAdi, @ExcelSatirSayisi, @MysqlUrunSayisi, @EklendiBayit, @Guncellenen, @Degismeyen, @Atlandi, @HataliSatir, @UyariSatir)", conn);
        cmd.Parameters.AddWithValue("@IslemTipi", islemTipi);
        cmd.Parameters.AddWithValue("@DosyaAdi", result.SourceFileName ?? string.Empty);
        cmd.Parameters.AddWithValue("@KullaniciAdi", kullaniciAdi);
        cmd.Parameters.AddWithValue("@ExcelSatirSayisi", result.ExcelSatirSayisi);
        cmd.Parameters.AddWithValue("@MysqlUrunSayisi", result.MysqlUrunSayisi);
        cmd.Parameters.AddWithValue("@EklendiBayit", result.EklendiBayit);
        cmd.Parameters.AddWithValue("@Guncellenen", result.Guncellenen);
        cmd.Parameters.AddWithValue("@Degismeyen", result.Degismeyen);
        cmd.Parameters.AddWithValue("@Atlandi", result.Atlandi);
        cmd.Parameters.AddWithValue("@HataliSatir", result.HataliSatir);
        cmd.Parameters.AddWithValue("@UyariSatir", result.UyariSatir);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<List<UrunTopluYukleLogItem>> GetRecentUrunTopluYukleLogsAsync(MySqlConnection conn)
    {
        var logs = new List<UrunTopluYukleLogItem>();
        using var cmd = new MySqlCommand(@"
            SELECT Id, IslemTipi, DosyaAdi, KullaniciAdi, ExcelSatirSayisi, MysqlUrunSayisi, EklendiBayit, Guncellenen, Degismeyen, Atlandi, HataliSatir, UyariSatir, OlusturmaTarihi
            FROM stk_UrunTopluYuklemeLog
            ORDER BY OlusturmaTarihi DESC
            LIMIT 10", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            logs.Add(new UrunTopluYukleLogItem
            {
                Id = reader.GetInt32("Id"),
                IslemTipi = reader.GetString("IslemTipi"),
                DosyaAdi = reader.GetString("DosyaAdi"),
                KullaniciAdi = reader.GetString("KullaniciAdi"),
                ExcelSatirSayisi = reader.GetInt32("ExcelSatirSayisi"),
                MysqlUrunSayisi = reader.GetInt32("MysqlUrunSayisi"),
                EklendiBayit = reader.GetInt32("EklendiBayit"),
                Guncellenen = reader.GetInt32("Guncellenen"),
                Degismeyen = reader.GetInt32("Degismeyen"),
                Atlandi = reader.GetInt32("Atlandi"),
                HataliSatir = reader.GetInt32("HataliSatir"),
                UyariSatir = reader.GetInt32("UyariSatir"),
                OlusturmaTarihi = Convert.ToDateTime(reader["OlusturmaTarihi"])
            });
        }

        return logs;
    }

    private async Task EnsureUrunKodColumnAsync(MySqlConnection conn)
    {
        var columnExistsQuery = @"
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'stk_Urun'
              AND COLUMN_NAME = 'Kod';";

        using (var checkColumnCmd = new MySqlCommand(columnExistsQuery, conn))
        {
            var columnExists = Convert.ToInt32(await checkColumnCmd.ExecuteScalarAsync()) > 0;
            if (!columnExists)
            {
                var addColumnQuery = "ALTER TABLE stk_Urun ADD COLUMN Kod VARCHAR(50) NULL AFTER Id;";
                using (var addColumnCmd = new MySqlCommand(addColumnQuery, conn))
                {
                    await addColumnCmd.ExecuteNonQueryAsync();
                }
            }
        }

        var indexExistsQuery = @"
            SELECT COUNT(*)
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'stk_Urun'
              AND INDEX_NAME = 'uq_stk_urun_kod';";

        using (var checkIndexCmd = new MySqlCommand(indexExistsQuery, conn))
        {
            var indexExists = Convert.ToInt32(await checkIndexCmd.ExecuteScalarAsync()) > 0;
            if (!indexExists)
            {
                var addIndexQuery = "CREATE UNIQUE INDEX uq_stk_urun_kod ON stk_Urun(Kod);";
                using (var addIndexCmd = new MySqlCommand(addIndexQuery, conn))
                {
                    await addIndexCmd.ExecuteNonQueryAsync();
                }
            }
        }
    }

    private async Task EnsureGrupColumnAsync(MySqlConnection conn)
    {
        var columnExistsQuery = @"
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'stk_Urun'
              AND COLUMN_NAME = 'Grup';";

        using (var checkColumnCmd = new MySqlCommand(columnExistsQuery, conn))
        {
            var columnExists = Convert.ToInt32(await checkColumnCmd.ExecuteScalarAsync()) > 0;
            if (!columnExists)
            {
                var addColumnQuery = "ALTER TABLE stk_Urun ADD COLUMN Grup VARCHAR(100) NULL AFTER Firma;";
                using (var addColumnCmd = new MySqlCommand(addColumnQuery, conn))
                {
                    await addColumnCmd.ExecuteNonQueryAsync();
                }
            }
        }
    }

    private async Task EnsureSubeResponsibleColumnAsync(MySqlConnection conn)
    {
        var columnQuery = @"
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'stk_Sube'
              AND COLUMN_NAME = 'SorumluKullaniciId';";

        using (var cmd = new MySqlCommand(columnQuery, conn))
        {
            var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0) > 0;
            if (!exists)
            {
                using (var alterCmd = new MySqlCommand("ALTER TABLE stk_Sube ADD COLUMN SorumluKullaniciId INT NULL AFTER AktifMi;", conn))
                {
                    await alterCmd.ExecuteNonQueryAsync();
                }
            }
        }
    }

    private static bool IsSafeTableName(string? tableName)
    {
        return !string.IsNullOrWhiteSpace(tableName) && Regex.IsMatch(tableName, "^[A-Za-z0-9_]+$");
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"`{identifier.Replace("`", "``")}`";
    }

    private async Task<List<string>> GetAllTableNamesAsync(MySqlConnection conn)
    {
        var result = new List<string>();
        const string query = @"
            SELECT TABLE_NAME
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME;";

        using var cmd = new MySqlCommand(query, conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString("TABLE_NAME"));
        }

        return result;
    }

    private async Task AppendCreateTableScriptAsync(MySqlConnection conn, string tableName, StringBuilder sb)
    {
        sb.AppendLine($"-- ----------------------------");
        sb.AppendLine($"-- Table structure for {tableName}");
        sb.AppendLine($"-- ----------------------------");
        sb.AppendLine($"DROP TABLE IF EXISTS {QuoteIdentifier(tableName)};");

        using var cmd = new MySqlCommand($"SHOW CREATE TABLE {QuoteIdentifier(tableName)};", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            sb.AppendLine(reader.GetString("Create Table") + ";");
            sb.AppendLine();
        }
    }

    private async Task AppendTableDataScriptAsync(MySqlConnection conn, string tableName, StringBuilder sb)
    {
        sb.AppendLine($"-- ----------------------------");
        sb.AppendLine($"-- Records of {tableName}");
        sb.AppendLine($"-- ----------------------------");

        using var cmd = new MySqlCommand($"SELECT * FROM {QuoteIdentifier(tableName)};", conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var values = new List<string>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                values.Add(ToSqlLiteral(reader.GetValue(i)));
            }

            sb.AppendLine($"INSERT INTO {QuoteIdentifier(tableName)} VALUES ({string.Join(", ", values)});");
        }

        sb.AppendLine();
    }

    private static string ToSqlLiteral(object value)
    {
        if (value == DBNull.Value) return "NULL";

        return value switch
        {
            string s => $"'{MySqlHelper.EscapeString(s)}'",
            char c => $"'{MySqlHelper.EscapeString(c.ToString())}'",
            bool b => b ? "1" : "0",
            byte[] bytes => "0x" + BitConverter.ToString(bytes).Replace("-", ""),
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss.ffffff}'",
            DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss.ffffff}'",
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? "NULL",
            _ => $"'{MySqlHelper.EscapeString(value.ToString() ?? string.Empty)}'"
        };
    }

    private static List<string> SplitSqlStatements(string sql)
    {
        var statements = new List<string>();
        var sb = new StringBuilder();
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inBacktick = false;

        for (var i = 0; i < sql.Length; i++)
        {
            var ch = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (!inSingleQuote && !inDoubleQuote && !inBacktick)
            {
                if (ch == '-' && next == '-')
                {
                    while (i < sql.Length && sql[i] != '\n') i++;
                    continue;
                }

                if (ch == '#')
                {
                    while (i < sql.Length && sql[i] != '\n') i++;
                    continue;
                }

                if (ch == '/' && next == '*')
                {
                    i += 2;
                    while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/')) i++;
                    i++;
                    continue;
                }
            }

            if (ch == '\'' && !inDoubleQuote && !inBacktick)
            {
                var escaped = i > 0 && sql[i - 1] == '\\';
                if (!escaped) inSingleQuote = !inSingleQuote;
            }
            else if (ch == '"' && !inSingleQuote && !inBacktick)
            {
                var escaped = i > 0 && sql[i - 1] == '\\';
                if (!escaped) inDoubleQuote = !inDoubleQuote;
            }
            else if (ch == '`' && !inSingleQuote && !inDoubleQuote)
            {
                inBacktick = !inBacktick;
            }

            if (ch == ';' && !inSingleQuote && !inDoubleQuote && !inBacktick)
            {
                var statement = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(statement))
                {
                    statements.Add(statement);
                }
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }

        var last = sb.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(last))
        {
            statements.Add(last);
        }

        return statements;
    }

    private static bool IsSessionLevelStatement(string statement)
    {
        var normalized = statement.Trim().ToUpperInvariant();
        return normalized.StartsWith("SET ") || normalized.StartsWith("START TRANSACTION") || normalized.StartsWith("COMMIT") || normalized.StartsWith("ROLLBACK");
    }

    private static string? TryExtractTargetTable(string statement)
    {
        var patterns = new[]
        {
            @"^INSERT\s+INTO\s+`?(?<table>[A-Za-z0-9_]+)`?",
            @"^REPLACE\s+INTO\s+`?(?<table>[A-Za-z0-9_]+)`?",
            @"^UPDATE\s+`?(?<table>[A-Za-z0-9_]+)`?",
            @"^DELETE\s+FROM\s+`?(?<table>[A-Za-z0-9_]+)`?",
            @"^CREATE\s+TABLE\s+(IF\s+NOT\s+EXISTS\s+)?`?(?<table>[A-Za-z0-9_]+)`?",
            @"^DROP\s+TABLE\s+(IF\s+EXISTS\s+)?`?(?<table>[A-Za-z0-9_]+)`?",
            @"^ALTER\s+TABLE\s+`?(?<table>[A-Za-z0-9_]+)`?",
            @"^TRUNCATE\s+TABLE\s+`?(?<table>[A-Za-z0-9_]+)`?",
            @"^LOCK\s+TABLES\s+`?(?<table>[A-Za-z0-9_]+)`?",
            @"^UNLOCK\s+TABLES"
        };

        var trimmed = statement.Trim();
        foreach (var pattern in patterns)
        {
            var match = Regex.Match(trimmed, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
            {
                if (!match.Groups.ContainsKey("table")) return null;
                var table = match.Groups["table"].Value;
                return string.IsNullOrWhiteSpace(table) ? null : table;
            }
        }

        return null;
    }

    // GET: Admin/UrunDuzenle/{id}
    public async Task<IActionResult> UrunDuzenle(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Home");

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureUrunKodColumnAsync(conn);
                await EnsureGrupColumnAsync(conn);

                var query = "SELECT Id, Kod, Ad, Barkod, Birim, Firma, Grup, AktifMi FROM stk_Urun WHERE Id = @Id";
                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var urun = new StsUrun
                            {
                                Id = reader.GetInt32("Id"),
                                Kod = reader["Kod"] == DBNull.Value ? null : reader["Kod"]?.ToString(),
                                Ad = reader.GetString("Ad"),
                                Barkod = reader["Barkod"] == DBNull.Value ? null : reader["Barkod"]?.ToString(),
                                Birim = reader.GetString("Birim"),
                                Firma = reader["Firma"] == DBNull.Value ? null : reader["Firma"]?.ToString(),
                                Grup = reader["Grup"] == DBNull.Value ? null : reader["Grup"]?.ToString(),
                                AktifMi = reader.GetBoolean("AktifMi")
                            };
                            return Json(urun);
                        }
                    }
                }
            }
            return Json(new { error = "Ürün bulunamadı." });
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    // POST: Admin/UrunGuncelle
    [HttpPost]
    public async Task<IActionResult> UrunGuncelle(int id, string kod, string ad, string barkod, string birim, string firma, string grup, bool aktifMi)
    {
        if (!IsAdmin()) return Unauthorized();

        if (string.IsNullOrWhiteSpace(ad) || string.IsNullOrWhiteSpace(birim))
            return Json(new { success = false, message = "Ürün adı ve birim zorunludur." });

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                await EnsureUrunKodColumnAsync(conn);
                await EnsureGrupColumnAsync(conn);

                var query = "UPDATE stk_Urun SET Kod = @Kod, Ad = @Ad, Barkod = @Barkod, Birim = @Birim, Firma = @Firma, Grup = @Grup, AktifMi = @AktifMi WHERE Id = @Id";
                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Kod", string.IsNullOrWhiteSpace(kod) ? (object)DBNull.Value : kod);
                    cmd.Parameters.AddWithValue("@Ad", ad);
                    cmd.Parameters.AddWithValue("@Barkod", string.IsNullOrWhiteSpace(barkod) ? (object)DBNull.Value : barkod);
                    cmd.Parameters.AddWithValue("@Birim", birim);
                    cmd.Parameters.AddWithValue("@Firma", string.IsNullOrWhiteSpace(firma) ? (object)DBNull.Value : firma);
                    cmd.Parameters.AddWithValue("@Grup", string.IsNullOrWhiteSpace(grup) ? (object)DBNull.Value : grup);
                    cmd.Parameters.AddWithValue("@AktifMi", aktifMi);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return Json(new { success = true, message = "Ürün başarıyla güncellendi." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Hata: {ex.Message}" });
        }
    }

    // POST: Admin/SilUrun
    [HttpPost]
    public async Task<IActionResult> SilUrun(int id)
    {
        if (!IsAdmin()) return Unauthorized();

        try
        {
            using (var conn = await _dbFactory.CreateConnectionAsync())
            {
                var query = "DELETE FROM stk_Urun WHERE Id = @Id";
                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    var rowsAffected = await cmd.ExecuteNonQueryAsync();
                    
                    if (rowsAffected == 0)
                        return Json(new { success = false, message = "Ürün bulunamadı." });
                }
            }

            return Json(new { success = true, message = "Ürün başarıyla silindi." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Hata: {ex.Message}" });
        }
    }
}
