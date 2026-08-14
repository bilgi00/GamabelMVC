// Models/PRS/MailModels.cs
using System;

namespace gamabelmvc.Models.PRS;

public class MailGonderimLog
{
    public int Id { get; set; }
    public int? TalimatId { get; set; }
    public int? FirmaId { get; set; }
    public string AliciEmail { get; set; } = string.Empty;
    public string Konu { get; set; } = string.Empty;
    public string? Icerik { get; set; }
    public string Durum { get; set; } = "Beklemede";
    public string? HataMesaji { get; set; }
    public DateTime GonderimTarihi { get; set; } = DateTime.Now;
    public bool OkunduMu { get; set; }
    public DateTime? OkunduTarihi { get; set; }
    public string? IpAdres { get; set; }
    public bool BasariliMi { get; set; }
}

public class MailSablonu
{
    public int Id { get; set; }
    public string SablonAdi { get; set; } = string.Empty;
    public string SablonKodu { get; set; } = string.Empty;
    public string Konu { get; set; } = string.Empty;
    public string Icerik { get; set; } = string.Empty;
    public bool AktifMi { get; set; } = true;
    public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
    public DateTime GuncellemeTarihi { get; set; } = DateTime.Now;
}

public class MailGonderModel
{
    public int TalimatId { get; set; }
    public List<int> FirmaIdleri { get; set; } = new();
    public string Konu { get; set; } = string.Empty;
    public string Mesaj { get; set; } = string.Empty;
    public string? CC { get; set; }
    public string? BCc { get; set; }
    public bool TestModu { get; set; }
    public string? TestEmail { get; set; }
}