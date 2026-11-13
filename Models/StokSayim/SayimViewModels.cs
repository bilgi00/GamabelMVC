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
