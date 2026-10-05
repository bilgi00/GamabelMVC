namespace gamabelmvc.Models.PRS;

public sealed class FaturaAraViewModel
{
    public string FaturaNo { get; set; } = string.Empty;
    public string FirmaAdi { get; set; } = string.Empty;
    public bool AramaYapildi { get; set; }
    public bool SonucSiniriUlasti { get; set; }
    public string? Hata { get; set; }
    public List<FaturaAraSonuc> Sonuclar { get; set; } = new();
}

public sealed class FaturaAraSonuc
{
    public int FaturaId { get; set; }
    public string FaturaNo { get; set; } = string.Empty;
    public string FirmaAdi { get; set; } = string.Empty;
    public decimal Bakiye { get; set; }
    public string OdemeDurumu { get; set; } = string.Empty;
    public string? DosyaAdi { get; set; }
    public int? TalimatId { get; set; }
    public string? TalimatNo { get; set; }
    public DateTime? OdemeTarihi { get; set; }
    public string? TalimatDurumu { get; set; }
    public string? BankaSubeAdi { get; set; }
    public decimal? TalimatSatirTutari { get; set; }
    public string? HazirlayanKullanici { get; set; }
    public string? OnaylayanKullanici { get; set; }
    public DateTime? OnayTarihi { get; set; }
}
