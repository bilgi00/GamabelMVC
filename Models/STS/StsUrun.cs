namespace gamabelmvc.Models.STS;

public class StsUrun
{
    public int Id { get; set; }
    public string? Kod { get; set; }
    public string Ad { get; set; } = null!;
    public string? Barkod { get; set; }
    public string Birim { get; set; } = "Adet";
    public string? Firma { get; set; }
    public string? Grup { get; set; }
    public bool AktifMi { get; set; } = true;
}
