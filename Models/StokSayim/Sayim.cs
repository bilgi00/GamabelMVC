namespace gamabelmvc.Models.StokSayim;

public class Sayim
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string Durum { get; set; } = "Açık";
    public DateTime AcilisZamani { get; set; }
    public DateTime? KapanisZamani { get; set; }
    public string OlusturanKullanici { get; set; } = string.Empty;
    public string? Aciklama { get; set; }

    public ICollection<SayimSistemStok> SistemStoklari { get; set; } = new List<SayimSistemStok>();
    public ICollection<SayimHareketi> Hareketler { get; set; } = new List<SayimHareketi>();
    public ICollection<SayimYuklemeGrubu> YuklemeGruplari { get; set; } = new List<SayimYuklemeGrubu>();
}
