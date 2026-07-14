namespace gamabelmvc.Models;

public class UrunTopluYukleResult
{
    public int EklendiBayit { get; set; }
    public int Guncellenen { get; set; }
    public int Degismeyen { get; set; }
    public int Atlandi { get; set; }
    public int HataliSatir { get; set; }
    public int UyariSatir { get; set; }
    public int SilinecekKayit { get; set; }
    public int ExcelSatirSayisi { get; set; }
    public int MysqlUrunSayisi { get; set; }
    public bool IsPreview { get; set; }
    public bool CanApply { get; set; }
    public bool DeleteMissingProducts { get; set; }
    public string? PreviewToken { get; set; }
    public string? SourceFileName { get; set; }
    public List<HataliUrunSatiri> HataliSatirlar { get; set; } = new List<HataliUrunSatiri>();
    public List<UyariUrunSatiri> UyariSatirlar { get; set; } = new List<UyariUrunSatiri>();
    public List<SilinecekUrunSatiri> SilinecekUrunler { get; set; } = new List<SilinecekUrunSatiri>();
    public List<UrunTopluYuklePlanItem> GuncellenecekUrunler { get; set; } = new List<UrunTopluYuklePlanItem>();
    public List<UrunTopluYukleLogItem> RecentLogs { get; set; } = new List<UrunTopluYukleLogItem>();
}

public class HataliUrunSatiri
{
    public int SatirNumarasi { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string HataNedeni { get; set; } = string.Empty;
}

public class UyariUrunSatiri
{
    public int SatirNumarasi { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string UyariNedeni { get; set; } = string.Empty;
}

public class UrunTopluYuklePlan
{
    public string Token { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int ExcelSatirSayisi { get; set; }
    public int MysqlUrunSayisi { get; set; }
    public int EklendiBayit { get; set; }
    public int Guncellenen { get; set; }
    public int Degismeyen { get; set; }
    public int Atlandi { get; set; }
    public int HataliSatir { get; set; }
    public int UyariSatir { get; set; }
    public int SilinecekKayit { get; set; }
    public List<string> KeepCodes { get; set; } = new List<string>();
    public List<HataliUrunSatiri> HataliSatirlar { get; set; } = new List<HataliUrunSatiri>();
    public List<UyariUrunSatiri> UyariSatirlar { get; set; } = new List<UyariUrunSatiri>();
    public List<SilinecekUrunSatiri> SilinecekUrunler { get; set; } = new List<SilinecekUrunSatiri>();
    public List<UrunTopluYuklePlanItem> Items { get; set; } = new List<UrunTopluYuklePlanItem>();
}

public class SilinecekUrunSatiri
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public string Grup { get; set; } = string.Empty;
    public string Barkod { get; set; } = string.Empty;
}

public class UrunTopluYuklePlanItem
{
    public int SatirNumarasi { get; set; }
    public string IslemTuru { get; set; } = string.Empty;
    public int? UrunId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public string Grup { get; set; } = string.Empty;
    public string Barkod { get; set; } = string.Empty;
    public string MevcutAd { get; set; } = string.Empty;
    public string MevcutBirim { get; set; } = string.Empty;
    public string MevcutGrup { get; set; } = string.Empty;
    public string MevcutBarkod { get; set; } = string.Empty;
    public bool BarkodDegisecek { get; set; }
}

public class UrunTopluYukleLogItem
{
    public int Id { get; set; }
    public string IslemTipi { get; set; } = string.Empty;
    public string DosyaAdi { get; set; } = string.Empty;
    public string KullaniciAdi { get; set; } = string.Empty;
    public int ExcelSatirSayisi { get; set; }
    public int MysqlUrunSayisi { get; set; }
    public int EklendiBayit { get; set; }
    public int Guncellenen { get; set; }
    public int Degismeyen { get; set; }
    public int Atlandi { get; set; }
    public int HataliSatir { get; set; }
    public int UyariSatir { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
}
