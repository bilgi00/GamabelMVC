namespace gamabelmvc.Models.StokSayim;

public class SayimSistemStok
{
    public int Id { get; set; }
    public int SayimId { get; set; }
    public string? StokKodu { get; set; }
    public string? ReferansKodu { get; set; }
    public string? Barkod { get; set; }
    public string UrunAdi { get; set; } = string.Empty;
    public decimal? GirenMiktar { get; set; }
    public decimal? CikanMiktar { get; set; }
    public decimal SistemStok { get; set; }
    public string? Birim { get; set; }
    public decimal? AlisFiyati { get; set; }
    public decimal? SatisFiyati { get; set; }
    public string? PCinsi { get; set; }

    public Sayim Sayim { get; set; } = null!;
}
