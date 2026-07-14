namespace gamabelmvc.Models.STS;

public class StsKullanici
{
    public int Id { get; set; }
    public int SubeId { get; set; }
    public string AdSoyad { get; set; } = null!;
    public string KullaniciAdi { get; set; } = null!;
    public string SifreHash { get; set; } = null!;
    public string Rol { get; set; } = "SubePersoneli"; // SubePersoneli, DepoSorumlusu, Admin
    public DateTime? SonGirisTarihi { get; set; }
    public bool AktifMi { get; set; } = true;
    
    // Navigation
    public StsSube? Sube { get; set; }
}
