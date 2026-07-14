namespace gamabelmvc.Models.STS;

public class StsSevkiyat
{
    public int Id { get; set; }
    public int EksikKaydiId { get; set; }
    public decimal SevkMiktari { get; set; }
    public DateTime SevkTarihi { get; set; } = DateTime.Now;
    public int SevkedenKullaniciId { get; set; }
    public string Durum { get; set; } = "Hazirlaniyor"; // Hazirlaniyor, Yolda, TeslimEdildi, OnayBekliyor, Onaylandi
    public DateTime? SubeOnayTarihi { get; set; }
    public string? SubeOnayNotu { get; set; }
    
    // Navigation
    public StsEksikKaydi? EksikKaydi { get; set; }
    public StsKullanici? SevkedenKullanici { get; set; }
}
