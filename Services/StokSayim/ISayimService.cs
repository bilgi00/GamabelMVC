using gamabelmvc.Models.StokSayim;

namespace gamabelmvc.Services.StokSayim;

public interface ISayimService
{
    Task<IReadOnlyList<SayimListeSatiriViewModel>> GetSayimlarAsync(CancellationToken cancellationToken = default);
    Task<int> SayimOlusturAsync(SayimOlusturViewModel model, string kullanici, CancellationToken cancellationToken = default);
    Task<SayimDetayViewModel?> GetDetayAsync(int id, CancellationToken cancellationToken = default);
    Task<SayimEkraniViewModel?> GetSayimEkraniAsync(
        int id,
        string? barkod,
        string? urunAdi,
        int? stokId,
        int? hareketId,
        CancellationToken cancellationToken = default);
    Task<string> SayimEkleAsync(
        int id,
        int? stokId,
        string? barkod,
        decimal miktar,
        string kullanici,
        CancellationToken cancellationToken = default);
    Task SayimHareketiGuncelleAsync(
        int id,
        int hareketId,
        decimal miktar,
        string kullanici,
        CancellationToken cancellationToken = default);
    Task SayimHareketiIptalEtAsync(
        int id,
        int hareketId,
        string kullanici,
        CancellationToken cancellationToken = default);
    Task<SayimRaporViewModel?> GetSayimRaporuAsync(int id, CancellationToken cancellationToken = default);
    Task SayimKapatAsync(int id, string kullanici, CancellationToken cancellationToken = default);
    Task SayimYenidenAcAsync(int id, string kullanici, CancellationToken cancellationToken = default);
}
