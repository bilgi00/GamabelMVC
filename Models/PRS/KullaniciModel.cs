namespace gamabelmvc.Models.PRS;

public class KullaniciModel
{
    public int Id { get; set; }
    public string? KullaniciAdi { get; set; }
    public string? Sifre { get; set; }
    public string? Ad { get; set; }
    public string? Soyad { get; set; }
    public string? Telefon { get; set; }
    public string? Email { get; set; }
    public string? Birim { get; set; }
    public string? Rol { get; set; }
    public bool AktifMi { get; set; } = true;
}