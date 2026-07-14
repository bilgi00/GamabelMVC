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
    // -----------------------------------------------------------------------
    public async Task<List<OtAcikFatura>> GetBatchFaturalariAsync(int batchId)
    {
        var result = new List<OtAcikFatura>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        var cmd = new MySqlCommand(
            @"SELECT id, cari_kart, fatura_no, bakiye
              FROM prs_ot_acik_faturalar
              WHERE import_batch_id = @bid AND odemeye_dahil_edildi = 0
              ORDER BY cari_kart, fatura_no",
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
                Bakiye = reader.GetDecimal(3)
            });
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // TALİMAT OLUŞTUR
    // -----------------------------------------------------------------------
    public async Task<OtTalimat> TalimatOlusturAsync(
        int batchId,
        List<int> secilenFaturaIdleri,
        int bankaId,
        string hazirlayanKullanici)
    {
        if (batchId <= 0 || bankaId <= 0)
            throw new InvalidOperationException("Gecerli yukleme ve banka secilmelidir.");

        secilenFaturaIdleri ??= new List<int>();
        secilenFaturaIdleri = secilenFaturaIdleri.Distinct().ToList();
        if (secilenFaturaIdleri == null || secilenFaturaIdleri.Count == 0)
            throw new InvalidOperationException("Ödemeye dahil edilecek fatura seçilmedi.");

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        // Fatura detayları
        var inClause = string.Join(",", secilenFaturaIdleri.Select((_, i) => $"@fid{i}"));
        var faturaCmd = new MySqlCommand(
            $"SELECT id, cari_kart, fatura_no, bakiye FROM prs_ot_acik_faturalar WHERE import_batch_id = @batchId AND odemeye_dahil_edildi = 0 AND id IN ({inClause}) FOR UPDATE",
            connection, tx);
        faturaCmd.Parameters.AddWithValue("@batchId", batchId);
        for (int i = 0; i < secilenFaturaIdleri.Count; i++)
            faturaCmd.Parameters.AddWithValue($"@fid{i}", secilenFaturaIdleri[i]);

        var faturalar = new List<OtAcikFatura>();
        await using (var r = await faturaCmd.ExecuteReaderAsync())
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
            throw new InvalidOperationException("Seçilen faturalar bulunamadı.");

        // Banka bilgisi
        var bankaCmd = new MySqlCommand(
            "SELECT sube_adi, iban FROM prs_ot_bankalar WHERE id = @id", connection, tx);
        bankaCmd.Parameters.AddWithValue("@id", bankaId);
        string bankaSubeAdi = "", bankaIBAN = "";
        await using (var r = await bankaCmd.ExecuteReaderAsync())
        {
            if (!await r.ReadAsync())
                throw new InvalidOperationException("Seçilen banka bulunamadı.");
            bankaSubeAdi = r.GetString(0);
            bankaIBAN = r.GetString(1);
        }

        // Sıra numarası (yıl bazlı)
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

        try
        {
            // Talimat başlığı
            var talimatCmd = new MySqlCommand(
                @"INSERT INTO prs_ot_talimatlar
                  (talimat_no, tarih, banka_id, toplam_tutar, toplam_adet, hazirlayan_kullanici)
                  VALUES (@no, NOW(), @banka, 0, 0, @hazirlayan)",
                connection, tx);
            talimatCmd.Parameters.AddWithValue("@no", talimatNo);
            talimatCmd.Parameters.AddWithValue("@banka", bankaId);
            talimatCmd.Parameters.AddWithValue("@hazirlayan", hazirlayanKullanici);
            await talimatCmd.ExecuteNonQueryAsync();
            var talimatId = (int)talimatCmd.LastInsertedId;

            decimal toplamTutar = 0;
            int toplamAdet = 0;

            foreach (var grup in faturalar.GroupBy(f => f.CariKart))
            {
                // Firma IBAN bul
                var firmaCmd = new MySqlCommand(
                    "SELECT id, odeme_ismi, iban FROM prs_ot_firmalar WHERE cari_ismi = @cari LIMIT 1",
                    connection, tx);
                firmaCmd.Parameters.AddWithValue("@cari", grup.Key);
                int firmaId; string odemeIsmi, firmaIBAN;
                await using (var fr = await firmaCmd.ExecuteReaderAsync())
                {
                    if (!await fr.ReadAsync())
                        throw new InvalidOperationException(
                            $"'{grup.Key}' için Firmalar listesinde IBAN kaydı bulunamadı.");
                    firmaId = fr.GetInt32(0);
                    odemeIsmi = fr.GetString(1);
                    firmaIBAN = fr.GetString(2);
                }

                var tutar = grup.Sum(f => f.Bakiye);
                var faturaNoListesi = string.Join(", ", grup.Select(f => f.FaturaNo));

                var satirCmd = new MySqlCommand(
                    @"INSERT INTO prs_ot_talimat_satirlari (talimat_id, firma_id, aciklama, tutar)
                      VALUES (@tid, @fid, @aciklama, @tutar)",
                    connection, tx);
                satirCmd.Parameters.AddWithValue("@tid", talimatId);
                satirCmd.Parameters.AddWithValue("@fid", firmaId);
                satirCmd.Parameters.AddWithValue("@aciklama", $"FATURA NO: {faturaNoListesi}");
                satirCmd.Parameters.AddWithValue("@tutar", tutar);
                await satirCmd.ExecuteNonQueryAsync();
                var satirId = (int)satirCmd.LastInsertedId;

                foreach (var f in grup)
                {
                    var iliskiCmd = new MySqlCommand(
                        "INSERT INTO prs_ot_talimat_satiri_faturalari (talimat_satiri_id, acik_fatura_id) VALUES (@sid, @afid)",
                        connection, tx);
                    iliskiCmd.Parameters.AddWithValue("@sid", satirId);
                    iliskiCmd.Parameters.AddWithValue("@afid", f.Id);
                    await iliskiCmd.ExecuteNonQueryAsync();

                    var updateCmd = new MySqlCommand(
                        "UPDATE prs_ot_acik_faturalar SET odemeye_dahil_edildi = 1 WHERE id = @id AND import_batch_id = @batchId AND odemeye_dahil_edildi = 0",
                        connection, tx);
                    updateCmd.Parameters.AddWithValue("@id", f.Id);
                    updateCmd.Parameters.AddWithValue("@batchId", batchId);
                    if (await updateCmd.ExecuteNonQueryAsync() != 1)
                        throw new InvalidOperationException("Fatura daha once baska bir talimata dahil edilmis.");
                }

                toplamTutar += tutar;
                toplamAdet++;
            }

            // Toplamları güncelle
            var guncelleCmd = new MySqlCommand(
                "UPDATE prs_ot_talimatlar SET toplam_tutar = @tutar, toplam_adet = @adet WHERE id = @id",
                connection, tx);
            guncelleCmd.Parameters.AddWithValue("@tutar", toplamTutar);
            guncelleCmd.Parameters.AddWithValue("@adet", toplamAdet);
            guncelleCmd.Parameters.AddWithValue("@id", talimatId);
            await guncelleCmd.ExecuteNonQueryAsync();

            await tx.CommitAsync();

            return new OtTalimat
            {
                Id = talimatId,
                TalimatNo = talimatNo,
                Tarih = DateTime.Now,
                BankaId = bankaId,
                BankaSubeAdi = bankaSubeAdi,
                BankaIBAN = bankaIBAN,
                ToplamTutar = toplamTutar,
                ToplamAdet = toplamAdet,
                HazirlayanKullanici = hazirlayanKullanici
            };
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
                     t.toplam_tutar, t.toplam_adet, t.hazirlayan_kullanici, t.onaylayan_kullanici
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
                    OnaylayanKullanici = r.IsDBNull(9) ? null : r.GetString(9)
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
    // BANKA / FİRMA YÖNETİMİ
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

    public async Task<List<OtFirma>> GetFirmalarAsync()
    {
        var result = new List<OtFirma>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        var cmd = new MySqlCommand(
            "SELECT id, cari_ismi, odeme_ismi, iban, aciklama FROM prs_ot_firmalar ORDER BY cari_ismi", connection);
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
}
