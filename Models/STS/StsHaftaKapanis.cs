namespace gamabelmvc.Models.STS;

public class StsHaftaKapanis
{
    public int Id { get; set; }
    public string HaftaNo { get; set; } = null!;
    public DateTime PazartesiTarihi { get; set; }
    public bool EksikGirisKapandiMi { get; set; } = false;
    public string? GecGirenSubeler { get; set; } // JSON
}
