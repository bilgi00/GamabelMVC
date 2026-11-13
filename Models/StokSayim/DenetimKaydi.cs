namespace gamabelmvc.Models.StokSayim;

public class DenetimKaydi
{
    public int Id { get; set; }
    public DateTime Zaman { get; set; }
    public string Kullanici { get; set; } = string.Empty;
    public string Islem { get; set; } = string.Empty;
    public int? HedefId { get; set; }
    public string? Detay { get; set; }
}
