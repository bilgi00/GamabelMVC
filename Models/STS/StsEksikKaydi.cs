namespace gamabelmvc.Models.STS;

public class StsEksikKaydi
{
    public int Id { get; set; }
    public string? SiparisNo { get; set; }
    public int SubeId { get; set; }
    public int UrunId { get; set; }
    public decimal Miktar { get; set; }
    public bool AcilMi { get; set; } = false;
    public string? Not { get; set; }
    public bool GeciktiMi { get; set; }
    public bool TekrarliMi { get; set; }
    public int TekrarHaftaSayisi { get; set; }
    public string Durum { get; set; } = "Bekliyor"; // Bekliyor, SevkEdildi, Tamamlandi
    public string HaftaNo { get; set; } = null!;
    public DateTime GirisTarihi { get; set; } = DateTime.Now;
    public int GirisiYapanKullaniciId { get; set; }
    public DateTime SonGuncellemeTarihi { get; set; } = DateTime.Now;
    
    // Navigation
    public StsSube? Sube { get; set; }
    public StsUrun? Urun { get; set; }
    public StsKullanici? GirisiYapanKullanici { get; set; }
    public ICollection<StsSevkiyat> Sevkiyatlar { get; set; } = new List<StsSevkiyat>();
}
