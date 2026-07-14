namespace gamabelmvc.Models.STS;

public class StsFabrikaSiparisi
{
    public int Id { get; set; }
    public int? KaynakSevkiyatId { get; set; }
    public int UrunId { get; set; }
    public decimal Miktar { get; set; }
    public string HaftaNo { get; set; } = null!;
    public DateTime SiparisTarihi { get; set; } = DateTime.Now;
    public string Durum { get; set; } = "SiparisVerildi"; // SiparisVerildi, Uretimde, Yolda, TeslimAlindi
    public DateTime? TahminiTeslimTarihi { get; set; }
    public string? Not { get; set; }
    
    // Navigation
    public StsUrun? Urun { get; set; }
}
