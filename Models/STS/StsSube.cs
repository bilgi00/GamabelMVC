namespace gamabelmvc.Models.STS;

public class StsSube
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
    public string Kod { get; set; } = null!;
    public string Tip { get; set; } = "Sube"; // Sube, AnaDepo
    public bool AktifMi { get; set; } = true;
    public int? SorumluKullaniciId { get; set; }
    public string? SorumluAdi { get; set; }
    public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
}
