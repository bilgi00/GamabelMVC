namespace gamabelmvc.Models.STS;

public class StsHareketLog
{
    public int Id { get; set; }
    public string TabloAdi { get; set; } = null!;
    public int KayitId { get; set; }
    public string? EskiDeger { get; set; }
    public string? YeniDeger { get; set; }
    public string IslemTipi { get; set; } = null!; // Ekle, Guncelle, Sil, Duzeltme
    public int YapanKullaniciId { get; set; }
    public DateTime IslemTarihi { get; set; } = DateTime.Now;
    public string? IpAdres { get; set; }
    
    // Navigation
    public StsKullanici? YapanKullanici { get; set; }
}
