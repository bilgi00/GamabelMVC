namespace gamabelmvc.Models.StokSayim;

public class SayimYuklemeGrubu
{
    public int Id { get; set; }
    public int SayimId { get; set; }
    public string Tur { get; set; } = string.Empty;
    public string DosyaAdi { get; set; } = string.Empty;
    public string Kullanici { get; set; } = string.Empty;
    public DateTime Zaman { get; set; }
    public int SatirSayisi { get; set; }
    public int HataliSatirSayisi { get; set; }

    public Sayim Sayim { get; set; } = null!;
    public ICollection<SayimHareketi> Hareketler { get; set; } = new List<SayimHareketi>();
}
