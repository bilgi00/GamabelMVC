using System.Text.Json;
using MySqlConnector;
using gamabelmvc.Models.PRS;

namespace gamabelmvc.Services;

public class OdemeTalimatService
{
    private readonly string _connectionString;

    public OdemeTalimatService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MyConnection")!;
    }

    // -----------------------------------------------------------------------
    // ANA LISTE
    // -----------------------------------------------------------------------
    public async Task<List<OtImportBatch>> GetSonYuklemelerAsync(int limit = 10)
    {
        var result = new List<OtImportBatch>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        var cmd = new MySqlCommand(
            "SELECT id, dosya_adi, yukleme_tarihi, satir_sayisi FROM prs_ot_import_batchlari ORDER BY yukleme_tarihi DESC LIMIT @limit",
            connection);
        cmd.Parameters.AddWithValue("@limit", limit);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new OtImportBatch
            {
                Id = reader.GetInt32(0),
                DosyaAdi = reader.GetString(1),
                YuklemeTarihi = reader.GetDateTime(2),
                SatirSayisi = reader.GetInt32(3)
            });
        }
        return result;
    }

    public async Task<List<OtTalimat>> GetSonTalimatlarAsync(int limit = 10)
    {
        var result = new List<OtTalimat>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        var cmd = new MySqlCommand(
            @"SELECT t.id, t.talimat_no, t.tarih, t.banka_id, b.sube_adi,
                     t.toplam_tutar, t.toplam_adet, t.hazirlayan_kullanici
              FROM prs_ot_talimatlar t
              LEFT JOIN prs_ot_bankalar b ON b.id = t.banka_id
              ORDER BY t.tarih DESC LIMIT @limit",
            connection);
        cmd.Parameters.AddWithValue("@limit", limit);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new OtTalimat
            {
                Id = reader.GetInt32(0),
                TalimatNo = reader.GetString(1),
                Tarih = reader.GetDateTime(2),
                BankaId = reader.GetInt32(3),
                BankaSubeAdi = reader.IsDBNull(4) ? "" : reader.GetString(4),
                ToplamTutar = reader.GetDecimal(5),
                ToplamAdet = reader.GetInt32(6),
                HazirlayanKullanici = reader.GetString(7)
            });
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // FATURA SEÇİM
    // -----------------------------------------------------------------------// -----------------------------------------------------------------------
// FATURA SEÇİM - TÜM FATURALAR (DURUM FİLTRESİZ)
// -----------------------------------------------------------------------
public async Task<List<OtAcikFatura>> GetBatchFaturalariAsync(int batchId)
{
    var result = new List<OtAcikFatura>();
    await using var connection = new MySqlConnection(_connectionString);
    await connection.OpenAsync();
    
    var cmd = new MySqlCommand(
        @"SELECT 
            f.id, 
            f.cari_kart, 
            f.fatura_no, 
            f.bakiye, 
            f.odeme_durumu,
            f.import_batch_id
          FROM prs_ot_acik_faturalar f
          WHERE f.import_batch_id = @bid
            AND LOWER(TRIM(COALESCE(f.odeme_durumu, ''))) = 'bekliyor'
            AND COALESCE(f.odemeye_dahil_edildi, 0) = 0
          ORDER BY f.cari_kart, f.fatura_no",
        connection);
    
    cmd.Parameters.AddWithValue("@bid", batchId);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        result.Add(new OtAcikFatura
        {
            Id = reader.GetInt32(0),
            CariKart = reader.GetString(1),
            FaturaNo = reader.GetString(2),
            Bakiye = reader.GetDecimal(3),
            OdemeDurumu = reader.IsDBNull(4) ? "" : reader.GetString(4),
            ImportBatchId = reader.GetInt32(5)
        });
    }
    return result;
}

public async Task<List<OtAcikFatura>> GetTumAcikFaturalarAsync()
{
    var result = new List<OtAcikFatura>();
    await using var connection = new MySqlConnection(_connectionString);
    await connection.OpenAsync();
    
    var cmd = new MySqlCommand(
        @"SELECT 
            f.id, 
            f.cari_kart, 
            f.fatura_no, 
            f.bakiye, 
            f.odeme_durumu,
            f.import_batch_id
          FROM prs_ot_acik_faturalar f
          WHERE LOWER(TRIM(COALESCE(f.odeme_durumu, ''))) = 'bekliyor'
            AND COALESCE(f.odemeye_dahil_edildi, 0) = 0
          ORDER BY f.cari_kart, f.fatura_no",
        connection);
    
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        result.Add(new OtAcikFatura
        {
            Id = reader.GetInt32(0),
            CariKart = reader.GetString(1),
            FaturaNo = reader.GetString(2),
            Bakiye = reader.GetDecimal(3),
            OdemeDurumu = reader.IsDBNull(4) ? "" : reader.GetString(4),
            ImportBatchId = reader.GetInt32(5)
        });
    }
    return result;
}

    // -----------------------------------------------------------------------
    // TALİMAT OLUŞTUR - SADECE GEÇİCİ OLUŞTUR (VERİTABANINA KAYIT YOK)
    // -----------------------------------------------------------------------
    public OtTalimat? TalimatOlusturGecici(
        List<int> secilenFaturaIdleri,
        int bankaId,
        string hazirlayanKullanici,
        int batchId = 0)
    {
        if (bankaId <= 0)
            throw new InvalidOperationException("Geçerli banka seçilmelidir.");

        secilenFaturaIdleri ??= new List<int>();
        secilenFaturaIdleri = secilenFaturaIdleri.Distinct().ToList();
        if (secilenFaturaIdleri.Count == 0)
            throw new InvalidOperationException("Ödemeye dahil edilecek fatura seçilmedi.");

        var talimat = new OtTalimat
        {
            Id = 0,
            TalimatNo = $"G{DateTime.Now:yy}/000",
            Tarih = DateTime.Now,
            BankaId = bankaId,
            BankaSubeAdi = "",
            BankaIBAN = "",
            ToplamTutar = 0,
            ToplamAdet = 0,
            HazirlayanKullanici = hazirlayanKullanici,
            Durum = "beklemede",
            Satirlar = new List<OtTalimatSatiri>()
        };

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();
        
        var inClause = string.Join(",", secilenFaturaIdleri.Select((_, i) => $"@fid{i}"));
        var faturaCmd = new MySqlCommand(
            $"SELECT id, cari_kart, fatura_no, bakiye FROM prs_ot_acik_faturalar WHERE LOWER(TRIM(COALESCE(odeme_durumu, ''))) = 'bekliyor' AND COALESCE(odemeye_dahil_edildi, 0) = 0 AND id IN ({inClause})",
            connection);
        for (int i = 0; i < secilenFaturaIdleri.Count; i++)
            faturaCmd.Parameters.AddWithValue($"@fid{i}", secilenFaturaIdleri[i]);

        var faturalar = new List<OtAcikFatura>();
        using (var r = faturaCmd.ExecuteReader())
        {
            while (r.Read())
            {
                faturalar.Add(new OtAcikFatura
                {
                    Id = r.GetInt32(0),
                    CariKart = r.GetString(1),
                    FaturaNo = r.GetString(2),
                    Bakiye = r.GetDecimal(3)
                });
            }
        }

        if (faturalar.Count != secilenFaturaIdleri.Count)
            throw new InvalidOperationException("Seçilen faturalar bulunamadı veya daha önce ödenmiş.");

        var bankaCmd = new MySqlCommand(
            "SELECT sube_adi, iban FROM prs_ot_bankalar WHERE id = @id", connection);
        bankaCmd.Parameters.AddWithValue("@id", bankaId);
        using (var r = bankaCmd.ExecuteReader())
        {
            if (r.Read())
            {
                talimat.BankaSubeAdi = r.GetString(0);
                talimat.BankaIBAN = r.GetString(1);
            }
        }

        decimal toplamTutar = 0;
        int toplamAdet = 0;

        foreach (var grup in faturalar.GroupBy(f => f.CariKart))
        {
            var firmaCmd = new MySqlCommand(
                "SELECT id, odeme_ismi, iban FROM prs_ot_firmalar WHERE cari_ismi = @cari LIMIT 1",
                connection);
            firmaCmd.Parameters.AddWithValue("@cari", grup.Key);
            int firmaId; string odemeIsmi, firmaIBAN;
            using (var fr = firmaCmd.ExecuteReader())
            {
                if (!fr.Read())
                    throw new InvalidOperationException($"'{grup.Key}' için Firmalar listesinde IBAN kaydı bulunamadı.");
                firmaId = fr.GetInt32(0);
                odemeIsmi = fr.GetString(1);
                firmaIBAN = fr.GetString(2);
            }

            var tutar = grup.Sum(f => f.Bakiye);
            var faturaNoListesi = string.Join(", ", grup.Select(f => f.FaturaNo));

            talimat.Satirlar.Add(new OtTalimatSatiri
            {
                Id = 0,
                TalimatId = 0,
                FirmaId = firmaId,
                FirmaOdemeIsmi = odemeIsmi,
                FirmaIBAN = firmaIBAN,
                Aciklama = $"FATURA NO: {faturaNoListesi}",
                Tutar = tutar
            });

            toplamTutar += tutar;
            toplamAdet++;
        }

        talimat.ToplamTutar = toplamTutar;
        talimat.ToplamAdet = toplamAdet;

        connection.Close();
        return talimat;
    }

    // -----------------------------------------------------------------------
    // TALİMAT KAYDET (VERİTABANINA KAYIT)
    // -----------------------------------------------------------------------
    public async Task<OtTalimat?> TalimatKaydetAsync(
        OtTalimat geciciTalimat,
        List<int> secilenFaturaIdleri,
        int batchId = 0)
    {
        if (geciciTalimat == null)
            throw new InvalidOperationException("Geçersiz talimat verisi.");

        if (secilenFaturaIdleri == null || secilenFaturaIdleri.Count == 0)
            throw new InvalidOperationException("Ödemeye dahil edilecek fatura seçilmedi.");

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        try
        {
            var inClause = string.Join(",", secilenFaturaIdleri.Select((_, i) => $"@fid{i}"));
            string faturaSql;
            if (batchId > 0)
            {
                faturaSql = $"SELECT id, cari_kart, fatura_no, bakiye FROM prs_ot_acik_faturalar WHERE import_batch_id = @batchId AND LOWER(TRIM(COALESCE(odeme_durumu, ''))) = 'bekliyor' AND COALESCE(odemeye_dahil_edildi, 0) = 0 AND id IN ({inClause}) FOR UPDATE";
            }
            else
            {
                faturaSql = $"SELECT id, cari_kart, fatura_no, bakiye FROM prs_ot_acik_faturalar WHERE LOWER(TRIM(COALESCE(odeme_durumu, ''))) = 'bekliyor' AND COALESCE(odemeye_dahil_edildi, 0) = 0 AND id IN ({inClause}) FOR UPDATE";
            }

            var faturaCmd = new MySqlCommand(faturaSql, connection, tx);
            if (batchId > 0)
                faturaCmd.Parameters.AddWithValue("@batchId", batchId);
            for (int i = 0; i < secilenFaturaIdleri.Count; i++)
                faturaCmd.Parameters.AddWithValue($"@fid{i}", secilenFaturaIdleri[i]);

            var faturalar = new List<OtAcikFatura>();
            using (var r = await faturaCmd.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    faturalar.Add(new OtAcikFatura
                    {
                        Id = r.GetInt32(0),
                        CariKart = r.GetString(1),
                        FaturaNo = r.GetString(2),
                        Bakiye = r.GetDecimal(3)
                    });
                }
            }

            if (faturalar.Count != secilenFaturaIdleri.Count)
                throw new InvalidOperationException("Seçilen faturalar bulunamadı veya daha önce ödenmiş.");

            var yil = DateTime.Now.Year;
            var siraBaslatCmd = new MySqlCommand(
                "INSERT IGNORE INTO prs_ot_talimat_siralari (yil, son_sira) VALUES (@yil, 0)", connection, tx);
            siraBaslatCmd.Parameters.AddWithValue("@yil", yil);
            await siraBaslatCmd.ExecuteNonQueryAsync();

            var siraCmd = new MySqlCommand(
                "UPDATE prs_ot_talimat_siralari SET son_sira = LAST_INSERT_ID(son_sira + 1) WHERE yil = @yil", connection, tx);
            siraCmd.Parameters.AddWithValue("@yil", yil);
            await siraCmd.ExecuteNonQueryAsync();

            var siraNo = Convert.ToInt32(await new MySqlCommand("SELECT LAST_INSERT_ID()", connection, tx).ExecuteScalarAsync());
            var talimatNo = $"G{DateTime.Now:yy}/{siraNo}";

            var talimatEkleCmd = new MySqlCommand(
                @"INSERT INTO prs_ot_talimatlar
                  (talimat_no, tarih, banka_id, toplam_tutar, toplam_adet, hazirlayan_kullanici, durum)
                  VALUES (@no, NOW(), @banka, @toplamTutar, @toplamAdet, @hazirlayan, 'beklemede')",
                connection, tx);
            talimatEkleCmd.Parameters.AddWithValue("@no", talimatNo);
            talimatEkleCmd.Parameters.AddWithValue("@banka", geciciTalimat.BankaId);
            talimatEkleCmd.Parameters.AddWithValue("@toplamTutar", geciciTalimat.ToplamTutar);
            talimatEkleCmd.Parameters.AddWithValue("@toplamAdet", geciciTalimat.ToplamAdet);
            talimatEkleCmd.Parameters.AddWithValue("@hazirlayan", geciciTalimat.HazirlayanKullanici);
            await talimatEkleCmd.ExecuteNonQueryAsync();
            var talimatId = (int)talimatEkleCmd.LastInsertedId;

            foreach (var satir in geciciTalimat.Satirlar)
            {
                var satirEkleCmd = new MySqlCommand(
                    @"INSERT INTO prs_ot_talimat_satirlari (talimat_id, firma_id, aciklama, tutar)
                      VALUES (@tid, @fid, @aciklama, @tutar)",
                    connection, tx);
                satirEkleCmd.Parameters.AddWithValue("@tid", talimatId);
                satirEkleCmd.Parameters.AddWithValue("@fid", satir.FirmaId);
                satirEkleCmd.Parameters.AddWithValue("@aciklama", satir.Aciklama);
                satirEkleCmd.Parameters.AddWithValue("@tutar", satir.Tutar);
                await satirEkleCmd.ExecuteNonQueryAsync();
                var satirId = (int)satirEkleCmd.LastInsertedId;

                var firmaKaydiCmd = new MySqlCommand(
                    "SELECT cari_ismi FROM prs_ot_firmalar WHERE id = @firmaId LIMIT 1",
                    connection,
                    tx);
                firmaKaydiCmd.Parameters.AddWithValue("@firmaId", satir.FirmaId);
                string? firmaCariIsmi = null;
                using (var firmaReader = await firmaKaydiCmd.ExecuteReaderAsync())
                {
                    if (await firmaReader.ReadAsync())
                    {
                        firmaCariIsmi = firmaReader.IsDBNull(0) ? null : firmaReader.GetString(0);
                    }
                }

                foreach (var f in faturalar.Where(f => string.Equals(f.CariKart, firmaCariIsmi, StringComparison.OrdinalIgnoreCase)))
                {
                    var iliskiCmd = new MySqlCommand(
                        "INSERT INTO prs_ot_talimat_satiri_faturalari (talimat_satiri_id, acik_fatura_id) VALUES (@sid, @afid)",
                        connection, tx);
                    iliskiCmd.Parameters.AddWithValue("@sid", satirId);
                    iliskiCmd.Parameters.AddWithValue("@afid", f.Id);
                    await iliskiCmd.ExecuteNonQueryAsync();

                    string updateSql;
                    if (batchId > 0)
                    {
                        updateSql = @"UPDATE prs_ot_acik_faturalar
                                      SET odemeye_dahil_edildi = @talimatNo,
                                          odeme_durumu = 'odendi'
                                      WHERE id = @id
                                        AND import_batch_id = @batchId
                                        AND (LOWER(TRIM(COALESCE(odeme_durumu, ''))) = 'bekliyor' OR LOWER(TRIM(COALESCE(odeme_durumu, ''))) = 'odendi' OR COALESCE(odemeye_dahil_edildi, 0) = 0)";
                    }
                    else
                    {
                        updateSql = @"UPDATE prs_ot_acik_faturalar
                                      SET odemeye_dahil_edildi = @talimatNo,
                                          odeme_durumu = 'odendi'
                                      WHERE id = @id
                                        AND (LOWER(TRIM(COALESCE(odeme_durumu, ''))) = 'bekliyor' OR LOWER(TRIM(COALESCE(odeme_durumu, ''))) = 'odendi' OR COALESCE(odemeye_dahil_edildi, 0) = 0)";
                    }

                    var updateCmd = new MySqlCommand(updateSql, connection, tx);
                    updateCmd.Parameters.AddWithValue("@id", f.Id);
                    updateCmd.Parameters.AddWithValue("@talimatNo", talimatNo);
                    if (batchId > 0)
                        updateCmd.Parameters.AddWithValue("@batchId", batchId);

                    var affectedRows = await updateCmd.ExecuteNonQueryAsync();
                    if (affectedRows != 1)
                        throw new InvalidOperationException("Fatura güncellenemedi. Durum veya kayıt koşulu uyuşmuyor.");
                }
            }

            await tx.CommitAsync();
            return await GetTalimatDetayAsync(talimatId);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // -----------------------------------------------------------------------
    // TALİMAT DETAY
    // -----------------------------------------------------------------------
    public async Task<OtTalimat?> GetTalimatDetayAsync(int id)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = new MySqlCommand(
            @"SELECT t.id, t.talimat_no, t.tarih, t.banka_id, b.sube_adi, b.iban,
                     t.toplam_tutar, t.toplam_adet, t.hazirlayan_kullanici, 
                     t.onaylayan_kullanici, t.onay_tarihi, t.durum
              FROM prs_ot_talimatlar t
              LEFT JOIN prs_ot_bankalar b ON b.id = t.banka_id
              WHERE t.id = @id",
            connection);
        cmd.Parameters.AddWithValue("@id", id);

        OtTalimat? talimat = null;
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            if (await r.ReadAsync())
            {
                talimat = new OtTalimat
                {
                    Id = r.GetInt32(0),
                    TalimatNo = r.GetString(1),
                    Tarih = r.GetDateTime(2),
                    BankaId = r.GetInt32(3),
                    BankaSubeAdi = r.IsDBNull(4) ? "" : r.GetString(4),
                    BankaIBAN = r.IsDBNull(5) ? "" : r.GetString(5),
                    ToplamTutar = r.GetDecimal(6),
                    ToplamAdet = r.GetInt32(7),
                    HazirlayanKullanici = r.GetString(8),
                    OnaylayanKullanici = r.IsDBNull(9) ? null : r.GetString(9),
                    OnayTarihi = r.IsDBNull(10) ? null : r.GetDateTime(10),
                    Durum = r.IsDBNull(11) ? "beklemede" : r.GetString(11),
                    Satirlar = new List<OtTalimatSatiri>()
                };
            }
        }

        if (talimat == null) return null;

        var satirCmd = new MySqlCommand(
            @"SELECT s.id, s.firma_id, f.odeme_ismi, f.iban, s.aciklama, s.tutar
              FROM prs_ot_talimat_satirlari s
              LEFT JOIN prs_ot_firmalar f ON f.id = s.firma_id
              WHERE s.talimat_id = @tid
              ORDER BY s.id",
            connection);
        satirCmd.Parameters.AddWithValue("@tid", id);
        await using var sr = await satirCmd.ExecuteReaderAsync();
        while (await sr.ReadAsync())
        {
            talimat.Satirlar.Add(new OtTalimatSatiri
            {
                Id = sr.GetInt32(0),
                FirmaId = sr.GetInt32(1),
                FirmaOdemeIsmi = sr.IsDBNull(2) ? "" : sr.GetString(2),
                FirmaIBAN = sr.IsDBNull(3) ? "" : sr.GetString(3),
                Aciklama = sr.GetString(4),
                Tutar = sr.GetDecimal(5)
            });
        }

        return talimat;
    }

    // -----------------------------------------------------------------------
    // BANKA YÖNETİMİ
    // -----------------------------------------------------------------------
    public async Task<List<OtBanka>> GetBankalarAsync()
    {
        var result = new List<OtBanka>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        var cmd = new MySqlCommand(
            "SELECT id, sube_adi, iban FROM prs_ot_bankalar ORDER BY sube_adi", connection);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            result.Add(new OtBanka
            {
                Id = r.GetInt32(0),
                SubeAdi = r.GetString(1),
                IBAN = r.GetString(2)
            });
        }
        return result;
    }

    public async Task BankaKaydetAsync(OtBanka banka)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        MySqlCommand cmd = banka.Id == 0
            ? new MySqlCommand("INSERT INTO prs_ot_bankalar (sube_adi, iban) VALUES (@sube, @iban)", connection)
            : new MySqlCommand("UPDATE prs_ot_bankalar SET sube_adi = @sube, iban = @iban WHERE id = @id", connection);
        if (banka.Id != 0) cmd.Parameters.AddWithValue("@id", banka.Id);
        cmd.Parameters.AddWithValue("@sube", banka.SubeAdi);
        cmd.Parameters.AddWithValue("@iban", banka.IBAN);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task BankaSilAsync(int id)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        var cmd = new MySqlCommand("DELETE FROM prs_ot_bankalar WHERE id = @id", connection);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // -----------------------------------------------------------------------
    // FİRMA YÖNETİMİ ✅ YENİ EKLENDİ
    // -----------------------------------------------------------------------
   // Services/OdemeTalimatService.cs

// Services/OdemeTalimatService.cs

public async Task<List<OtFirma>> GetFirmalarAsync()
{
    var result = new List<OtFirma>();
    await using var connection = new MySqlConnection(_connectionString);
    await connection.OpenAsync();
    var cmd = new MySqlCommand(
        "SELECT id, cari_ismi, odeme_ismi, iban, aciklama FROM prs_ot_firmalar ORDER BY cari_ismi", 
        connection);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        result.Add(new OtFirma
        {
            Id = r.GetInt32(0),
            CariIsmi = r.GetString(1),
            OdemeIsmi = r.GetString(2),
            IBAN = r.GetString(3),
            Aciklama = r.IsDBNull(4) ? null : r.GetString(4)
        });
    }
    return result;
}

    public async Task FirmaKaydetAsync(OtFirma firma)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        MySqlCommand cmd = firma.Id == 0
            ? new MySqlCommand(
                "INSERT INTO prs_ot_firmalar (cari_ismi, odeme_ismi, iban, aciklama) VALUES (@cari, @odeme, @iban, @aciklama)",
                connection)
            : new MySqlCommand(
                "UPDATE prs_ot_firmalar SET cari_ismi = @cari, odeme_ismi = @odeme, iban = @iban, aciklama = @aciklama WHERE id = @id",
                connection);
        if (firma.Id != 0) cmd.Parameters.AddWithValue("@id", firma.Id);
        cmd.Parameters.AddWithValue("@cari", firma.CariIsmi);
        cmd.Parameters.AddWithValue("@odeme", firma.OdemeIsmi);
        cmd.Parameters.AddWithValue("@iban", firma.IBAN);
        cmd.Parameters.AddWithValue("@aciklama", firma.Aciklama ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task FirmaSilAsync(int id)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        var cmd = new MySqlCommand("DELETE FROM prs_ot_firmalar WHERE id = @id", connection);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // Services/OdemeTalimatService.cs

// ================================================================
// FATURA SEÇİM - Batch'e Göre (CariKart = CariIsmi ile JOIN)
// ================================================================
public async Task<List<OtFaturaViewModel>> GetBatchFaturalariWithFirmaAsync(int batchId)
{
    var result = new List<OtFaturaViewModel>();
    await using var connection = new MySqlConnection(_connectionString);
    await connection.OpenAsync();
    
    var cmd = new MySqlCommand(
        @"SELECT 
            f.id, 
            f.cari_kart, 
            f.fatura_no, 
            f.bakiye, 
            f.odeme_durumu,
            f.import_batch_id,
            fm.odeme_ismi,
            fm.iban
          FROM prs_ot_acik_faturalar f
          LEFT JOIN prs_ot_firmalar fm 
            ON TRIM(UPPER(f.cari_kart)) = TRIM(UPPER(fm.cari_ismi))
          WHERE f.import_batch_id = @bid 
            AND LOWER(TRIM(COALESCE(f.odeme_durumu, ''))) = 'bekliyor'
            AND COALESCE(f.odemeye_dahil_edildi, 0) = 0
          ORDER BY f.cari_kart, f.fatura_no",
        connection);
    
    cmd.Parameters.AddWithValue("@bid", batchId);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        result.Add(new OtFaturaViewModel
        {
            Id = reader.GetInt32(0),
            CariKart = reader.GetString(1),
            FaturaNo = reader.GetString(2),
            Bakiye = reader.GetDecimal(3),
            OdemeDurumu = reader.IsDBNull(4) ? "bekliyor" : reader.GetString(4),
            ImportBatchId = reader.GetInt32(5),
            OdemeIsmi = reader.IsDBNull(6) ? null : reader.GetString(6),
            IBAN = reader.IsDBNull(7) ? null : reader.GetString(7)
        });
    }
    return result;
}

// ================================================================
// FATURA SEÇİM - Tüm Faturalar (CariKart = CariIsmi ile JOIN)
// ================================================================
public async Task<List<OtFaturaViewModel>> GetTumAcikFaturalarWithFirmaAsync()
{
    var result = new List<OtFaturaViewModel>();
    await using var connection = new MySqlConnection(_connectionString);
    await connection.OpenAsync();
    
    var cmd = new MySqlCommand(
        @"SELECT 
            f.id, 
            f.cari_kart, 
            f.fatura_no, 
            f.bakiye, 
            f.odeme_durumu,
            f.import_batch_id,
            fm.odeme_ismi,
            fm.iban
          FROM prs_ot_acik_faturalar f
          LEFT JOIN prs_ot_firmalar fm 
            ON TRIM(UPPER(f.cari_kart)) = TRIM(UPPER(fm.cari_ismi))
          WHERE LOWER(TRIM(COALESCE(f.odeme_durumu, ''))) = 'bekliyor'
            AND COALESCE(f.odemeye_dahil_edildi, 0) = 0
          ORDER BY f.cari_kart, f.fatura_no",
        connection);
    
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        result.Add(new OtFaturaViewModel
        {
            Id = reader.GetInt32(0),
            CariKart = reader.GetString(1),
            FaturaNo = reader.GetString(2),
            Bakiye = reader.GetDecimal(3),
            OdemeDurumu = reader.IsDBNull(4) ? "bekliyor" : reader.GetString(4),
            ImportBatchId = reader.GetInt32(5),
            OdemeIsmi = reader.IsDBNull(6) ? null : reader.GetString(6),
            IBAN = reader.IsDBNull(7) ? null : reader.GetString(7)
        });
    }
    return result;
}
}