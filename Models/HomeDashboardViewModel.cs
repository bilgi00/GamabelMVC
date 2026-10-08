namespace gamabelmvc.Models;

public class HomeDashboardViewModel
{
    public int Yil { get; set; }
    public int Ay { get; set; }
    public bool IzinListesiGoster { get; set; }
    public bool MesaiListesiGoster { get; set; }
    public string? HataMesaji { get; set; }
    public List<HomeIzinPersonelViewModel> IzinPersoneller { get; set; } = new();
    public List<HomeMesaiPersonelViewModel> MesaiPersoneller { get; set; } = new();
}

public class HomeIzinPersonelViewModel
{
    public string PersonelNo { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public int IzinGunu { get; set; }
    public string IzinTipleri { get; set; } = "";
}

public class HomeMesaiPersonelViewModel
{
    public string PersonelNo { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public int MesaiGunu { get; set; }
    public decimal FiiliSaat { get; set; }
    public decimal EkMesaiSaat { get; set; }
}
