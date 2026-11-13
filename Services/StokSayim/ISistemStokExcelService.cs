using gamabelmvc.Models.StokSayim;
using Microsoft.AspNetCore.Http;

namespace gamabelmvc.Services.StokSayim;

public interface ISistemStokExcelService
{
    byte[] SablonOlustur();

    Task<SistemStokOnizlemeViewModel> OnizleAsync(
        int sayimId,
        IFormFile dosya,
        string kullanici,
        CancellationToken cancellationToken = default);

    SistemStokOnizlemeViewModel? OnizlemeGetir(int sayimId, string token, string kullanici);

    byte[] HataliSatirlariExcelOlarakOlustur(int sayimId, string token, string kullanici);

    Task<int> OnizlemeyiKaydetAsync(
        int sayimId,
        string token,
        string kullanici,
        CancellationToken cancellationToken = default);
}
