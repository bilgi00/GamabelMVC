namespace gamabelmvc.Models.PRS;

public class LoginViewModel
{
    public string KullaniciAdi { get; set; } = string.Empty;
    public string Sifre { get; set; } = string.Empty;
    public string? Birim { get; set; }
}
