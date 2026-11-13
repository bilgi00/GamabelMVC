using gamabelmvc.Data;
using gamabelmvc.Models.StokSayim;
using Microsoft.EntityFrameworkCore;

namespace gamabelmvc.Services.StokSayim;

public sealed class SayimService(StokSayimDbContext dbContext) : ISayimService
{
    public async Task<IReadOnlyList<SayimListeSatiriViewModel>> GetSayimlarAsync(
        CancellationToken cancellationToken = default)
    {
        var sayimlar = await dbContext.Sayimlar
            .AsNoTracking()
            .OrderByDescending(s => s.AcilisZamani)
            .Select(s => new SayimListeSatiriViewModel
            {
                Id = s.Id,
                Ad = s.Ad,
                Durum = s.Durum,
                AcilisZamani = s.AcilisZamani,
                KapanisZamani = s.KapanisZamani,
                OlusturanKullanici = s.OlusturanKullanici
            })
            .ToListAsync(cancellationToken);

        if (sayimlar.Count == 0)
            return sayimlar;

        var stats = await GetSayimIstatistikleriAsync(null, cancellationToken);
        foreach (var sayim in sayimlar)
            UygulaIstatistik(sayim, stats, sayim.Id);

        return sayimlar;
    }

    public async Task<int> SayimOlusturAsync(
        SayimOlusturViewModel model,
        string kullanici,
        CancellationToken cancellationToken = default)
    {
        var ad = model.Ad?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ad))
            throw new InvalidOperationException("Sayım adı zorunludur.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var sayim = new Sayim
        {
            Ad = ad,
            Durum = "Açık",
            AcilisZamani = now,
            OlusturanKullanici = kullanici,
            Aciklama = string.IsNullOrWhiteSpace(model.Aciklama) ? null : model.Aciklama.Trim()
        };
        dbContext.Sayimlar.Add(sayim);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.DenetimKayitlari.Add(new DenetimKaydi
        {
            Zaman = now,
            Kullanici = kullanici,
            Islem = "Sayım açıldı",
            HedefId = sayim.Id,
            Detay = $"Sayım oluşturuldu: {sayim.Ad}"
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return sayim.Id;
    }

    public async Task<SayimDetayViewModel?> GetDetayAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var sayim = await dbContext.Sayimlar
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SayimDetayViewModel
            {
                Id = s.Id,
                Ad = s.Ad,
                Durum = s.Durum,
                AcilisZamani = s.AcilisZamani,
                KapanisZamani = s.KapanisZamani,
                OlusturanKullanici = s.OlusturanKullanici,
                Aciklama = s.Aciklama
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (sayim == null)
            return null;

        var stats = await GetSayimIstatistikleriAsync(id, cancellationToken);
        UygulaIstatistik(sayim, stats, id);

        sayim.DenetimKayitlari = await dbContext.DenetimKayitlari
            .AsNoTracking()
            .Where(k => k.HedefId == id)
            .OrderByDescending(k => k.Zaman)
            .Select(k => new SayimDenetimSatiriViewModel
            {
                Zaman = k.Zaman,
                Kullanici = k.Kullanici,
                Islem = k.Islem,
                Detay = k.Detay
            })
            .Take(20)
            .ToListAsync(cancellationToken);

        return sayim;
    }

    private async Task<SayimIstatistikleri> GetSayimIstatistikleriAsync(
        int? sayimId,
        CancellationToken cancellationToken)
    {
        var stokQuery = dbContext.SayimSistemStoklari.AsNoTracking().AsQueryable();
        var hareketQuery = dbContext.SayimHareketleri.AsNoTracking().Where(h => !h.Iptal);
        if (sayimId.HasValue)
        {
            stokQuery = stokQuery.Where(s => s.SayimId == sayimId.Value);
            hareketQuery = hareketQuery.Where(h => h.SayimId == sayimId.Value);
        }

        var toplamlar = await stokQuery
            .GroupBy(s => s.SayimId)
            .Select(g => new { SayimId = g.Key, Sayisi = g.Count() })
            .ToDictionaryAsync(x => x.SayimId, x => x.Sayisi, cancellationToken);

        var sayilanlar = await hareketQuery
            .GroupBy(h => h.SayimId)
            .Select(g => new
            {
                SayimId = g.Key,
                Sayisi = g.Select(h => h.Barkod).Distinct().Count()
            })
            .ToDictionaryAsync(x => x.SayimId, x => x.Sayisi, cancellationToken);

        var sayilmayanlarQuery = stokQuery.Where(stok =>
            !dbContext.SayimHareketleri.Any(h =>
                h.SayimId == stok.SayimId &&
                h.Barkod == stok.Barkod &&
                !h.Iptal));
        var sayilmayanlar = await sayilmayanlarQuery
            .GroupBy(s => s.SayimId)
            .Select(g => new { SayimId = g.Key, Sayisi = g.Count() })
            .ToDictionaryAsync(x => x.SayimId, x => x.Sayisi, cancellationToken);

        var yeniUrunQuery = hareketQuery.Where(h => h.YeniUrunMu);
        var yeniUrunler = await yeniUrunQuery
            .GroupBy(h => h.SayimId)
            .Select(g => new
            {
                SayimId = g.Key,
                Sayisi = g.Select(h => h.Barkod).Distinct().Count()
            })
            .ToDictionaryAsync(x => x.SayimId, x => x.Sayisi, cancellationToken);

        return new SayimIstatistikleri(toplamlar, sayilanlar, sayilmayanlar, yeniUrunler);
    }

    private static void UygulaIstatistik(
        SayimListeSatiriViewModel sayim,
        SayimIstatistikleri stats,
        int sayimId)
    {
        sayim.ToplamUrunSayisi = stats.Toplamlar.GetValueOrDefault(sayimId);
        sayim.SayilanUrunSayisi = stats.Sayilanlar.GetValueOrDefault(sayimId);
        sayim.SayilmayanUrunSayisi = stats.Sayilmayanlar.GetValueOrDefault(sayimId);
        sayim.SayimdaEklenenUrunSayisi = stats.YeniUrunler.GetValueOrDefault(sayimId);
    }

    private static void UygulaIstatistik(
        SayimDetayViewModel sayim,
        SayimIstatistikleri stats,
        int sayimId)
    {
        sayim.ToplamUrunSayisi = stats.Toplamlar.GetValueOrDefault(sayimId);
        sayim.SayilanUrunSayisi = stats.Sayilanlar.GetValueOrDefault(sayimId);
        sayim.SayilmayanUrunSayisi = stats.Sayilmayanlar.GetValueOrDefault(sayimId);
        sayim.SayimdaEklenenUrunSayisi = stats.YeniUrunler.GetValueOrDefault(sayimId);
    }

    private sealed record SayimIstatistikleri(
        Dictionary<int, int> Toplamlar,
        Dictionary<int, int> Sayilanlar,
        Dictionary<int, int> Sayilmayanlar,
        Dictionary<int, int> YeniUrunler);

    public Task SayimKapatAsync(int id, string kullanici, CancellationToken cancellationToken = default) =>
        DurumDegistirAsync(id, "Açık", "Kapalı", kullanici, cancellationToken);

    public Task SayimYenidenAcAsync(int id, string kullanici, CancellationToken cancellationToken = default) =>
        DurumDegistirAsync(id, "Kapalı", "Açık", kullanici, cancellationToken);

    private async Task DurumDegistirAsync(
        int id,
        string beklenenDurum,
        string yeniDurum,
        string kullanici,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var sayim = await dbContext.Sayimlar
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s.Id, s.Ad })
            .SingleOrDefaultAsync(cancellationToken);
        if (sayim == null)
            throw new InvalidOperationException("Sayım kaydı bulunamadı.");

        var now = DateTime.UtcNow;
        var affectedRows = await dbContext.Sayimlar
            .Where(s => s.Id == id && s.Durum == beklenenDurum)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Durum, yeniDurum)
                .SetProperty(s => s.KapanisZamani, yeniDurum == "Kapalı" ? now : null),
                cancellationToken);
        if (affectedRows == 0)
            throw new InvalidOperationException(
                yeniDurum == "Kapalı"
                    ? "Yalnızca açık sayım kapatılabilir."
                    : "Yalnızca kapalı sayım yeniden açılabilir.");

        dbContext.DenetimKayitlari.Add(new DenetimKaydi
        {
            Zaman = now,
            Kullanici = kullanici,
            Islem = yeniDurum == "Kapalı" ? "Sayım kapatıldı" : "Sayım yeniden açıldı",
            HedefId = sayim.Id,
            Detay = $"Sayım durumu {yeniDurum} olarak değiştirildi: {sayim.Ad}"
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
