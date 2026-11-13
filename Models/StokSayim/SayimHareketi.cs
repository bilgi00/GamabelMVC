namespace gamabelmvc.Models.StokSayim;

public class SayimHareketi
{
    public int Id { get; set; }
    public int SayimId { get; set; }
    public Guid? MobileRequestId { get; set; }
    public string Barkod { get; set; } = string.Empty;
    public string UrunAdi { get; set; } = string.Empty;
    public decimal Miktar { get; set; }
    public string KullaniciAdi { get; set; } = string.Empty;
    public DateTime Zaman { get; set; }
    public string? Aciklama { get; set; }
    public string Kaynak { get; set; } = "Ekran";
    public int? YuklemeGrubuId { get; set; }
    public bool YeniUrunMu { get; set; }
    public bool Iptal { get; set; }
    public string? IptalEden { get; set; }
    public DateTime? IptalZamani { get; set; }

    public Sayim Sayim { get; set; } = null!;
    public SayimYuklemeGrubu? YuklemeGrubu { get; set; }
}
