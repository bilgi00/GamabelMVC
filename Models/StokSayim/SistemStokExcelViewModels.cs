namespace gamabelmvc.Models.StokSayim;

public class SistemStokSatiriOnizlemeViewModel
{
    public int SatirNo { get; set; }
    public string? StokKodu { get; set; }
    public string? ReferansKodu { get; set; }
    public string Barkod { get; set; } = string.Empty;
    public string UrunAdi { get; set; } = string.Empty;
    public decimal? GirenMiktar { get; set; }
    public decimal? CikanMiktar { get; set; }
    public decimal? SistemStok { get; set; }
    public string? Birim { get; set; }
    public decimal? AlisFiyati { get; set; }
    public decimal? SatisFiyati { get; set; }
    public string? PCinsi { get; set; }
    public string Hata { get; set; } = string.Empty;
}

public class SistemStokOnizlemeViewModel
{
    public int SayimId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string DosyaAdi { get; set; } = string.Empty;
    public int ToplamSatirSayisi { get; set; }
    public int GecerliSatirSayisi { get; set; }
    public int HataliSatirSayisi { get; set; }
    public IReadOnlyList<SistemStokSatiriOnizlemeViewModel> GecerliSatirlar { get; set; } =
        Array.Empty<SistemStokSatiriOnizlemeViewModel>();
    public IReadOnlyList<SistemStokSatiriOnizlemeViewModel> HataliSatirlar { get; set; } =
        Array.Empty<SistemStokSatiriOnizlemeViewModel>();
    public bool Kaydedilebilir => GecerliSatirSayisi > 0 && HataliSatirSayisi == 0;
}
