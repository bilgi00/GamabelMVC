using System.ComponentModel.DataAnnotations;

namespace gamabelmvc.Models.StokSayim;

public class SayimOlusturViewModel
{
    [Required(ErrorMessage = "Sayım adı zorunludur.")]
    [StringLength(200, ErrorMessage = "Sayım adı en fazla 200 karakter olabilir.")]
    public string Ad { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Açıklama en fazla 2000 karakter olabilir.")]
    public string? Aciklama { get; set; }
}

public class SayimListeSatiriViewModel
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
    public DateTime AcilisZamani { get; set; }
    public DateTime? KapanisZamani { get; set; }
    public string OlusturanKullanici { get; set; } = string.Empty;
    public int ToplamUrunSayisi { get; set; }
    public int SayilanUrunSayisi { get; set; }
    public int SayilmayanUrunSayisi { get; set; }
    public int SayimdaEklenenUrunSayisi { get; set; }
}

public class SayimDenetimSatiriViewModel
{
    public DateTime Zaman { get; set; }
    public string Kullanici { get; set; } = string.Empty;
    public string Islem { get; set; } = string.Empty;
    public string? Detay { get; set; }
}

public class SayimDetayViewModel
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
    public DateTime AcilisZamani { get; set; }
    public DateTime? KapanisZamani { get; set; }
    public string OlusturanKullanici { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public int ToplamUrunSayisi { get; set; }
    public int SayilanUrunSayisi { get; set; }
    public int SayilmayanUrunSayisi { get; set; }
    public int SayimdaEklenenUrunSayisi { get; set; }
    public IReadOnlyList<SayimDenetimSatiriViewModel> DenetimKayitlari { get; set; } =
        Array.Empty<SayimDenetimSatiriViewModel>();
}

public class SayimEkraniViewModel
{
    public int SayimId { get; set; }
    public string SayimAdi { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
    public string? UrunArama { get; set; }
    public bool UrunAramaYapildi { get; set; }
    public int? SeciliStokId { get; set; }
    public string? Barkod { get; set; }
    public bool BarkodArandi { get; set; }
    public string? UrunAdi { get; set; }
    public string? StokKodu { get; set; }
    public string? Birim { get; set; }
    public decimal? KalanMiktar { get; set; }
    public IReadOnlyList<SayimEkraniUrunViewModel> UrunAramaSonuclari { get; set; } =
        Array.Empty<SayimEkraniUrunViewModel>();
    public IReadOnlyList<SayimEkraniHareketViewModel> SonHareketler { get; set; } =
        Array.Empty<SayimEkraniHareketViewModel>();
    public SayimEkraniHareketViewModel? DuzenlenecekHareket { get; set; }
}

public class SayimEkraniUrunViewModel
{
    public int Id { get; set; }
    public string? StokKodu { get; set; }
    public string? Barkod { get; set; }
    public string UrunAdi { get; set; } = string.Empty;
    public string? Birim { get; set; }
    public decimal SistemStok { get; set; }
}

public class SayimEkraniHareketViewModel
{
    public int Id { get; set; }
    public DateTime Zaman { get; set; }
    public string Barkod { get; set; } = string.Empty;
    public string UrunAdi { get; set; } = string.Empty;
    public decimal Miktar { get; set; }
    public string KullaniciAdi { get; set; } = string.Empty;
}

public class SayimRaporViewModel
{
    public int SayimId { get; set; }
    public string SayimAdi { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
    public decimal SistemStokToplami { get; set; }
    public decimal SayimToplami { get; set; }
    public decimal FarkToplami { get; set; }
    public IReadOnlyList<SayimRaporSatiriViewModel> Satirlar { get; set; } =
        Array.Empty<SayimRaporSatiriViewModel>();
}

public class SayimRaporSatiriViewModel
{
    public string? StokKodu { get; set; }
    public string? ReferansKodu { get; set; }
    public string? Barkod { get; set; }
    public string UrunAdi { get; set; } = string.Empty;
    public decimal? GirenMiktar { get; set; }
    public decimal? CikanMiktar { get; set; }
    public decimal KalanMiktar { get; set; }
    public string? Birim { get; set; }
    public decimal? AlisFiyati { get; set; }
    public decimal? SatisFiyati { get; set; }
    public string? PCinsi { get; set; }
    public decimal SayimMiktari { get; set; }
    public decimal Fark { get; set; }
    public bool SayildiMi { get; set; }
}
