using System;
using System.Collections.Generic;

namespace gamabelmvc.Models.PRS;

public class OtFirma
{
    public int Id { get; set; }
    public string CariIsmi { get; set; } = string.Empty;
    public string OdemeIsmi { get; set; } = string.Empty;
    public string IBAN { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
}

public class OtBanka
{
    public int Id { get; set; }
    public string SubeAdi { get; set; } = string.Empty;
    public string IBAN { get; set; } = string.Empty;
}

public class OtImportBatch
{
    public int Id { get; set; }
    public string DosyaAdi { get; set; } = string.Empty;
    public DateTime YuklemeTarihi { get; set; }
    public int SatirSayisi { get; set; }
}

public class OtFaturaViewModel
{
    public int Id { get; set; }
    public string CariKart { get; set; } = string.Empty;
    public string FaturaNo { get; set; } = string.Empty;
    public decimal Bakiye { get; set; }
    public string OdemeDurumu { get; set; } = "bekliyor";
    public int ImportBatchId { get; set; }
    
    // ✅ JOIN ile gelen firma bilgileri
    public string? OdemeIsmi { get; set; }
    public string? IBAN { get; set; }
}

public class OtAcikFatura
{
    public int Id { get; set; }
    public string CariKart { get; set; } = string.Empty;
    public string FaturaNo { get; set; } = string.Empty;
    public decimal Bakiye { get; set; }
    public bool OdemeyeDahilEdildi { get; set; }
    public string OdemeDurumu { get; set; } = "bekliyor";
    public int ImportBatchId { get; set; }
}



public class OtTalimat
{
    public int Id { get; set; }
    public string TalimatNo { get; set; } = "";
    public DateTime Tarih { get; set; }
    public int BankaId { get; set; }
    public string BankaSubeAdi { get; set; } = "";
    public string BankaIBAN { get; set; } = "";
    public decimal ToplamTutar { get; set; }
    public int ToplamAdet { get; set; }
    public string HazirlayanKullanici { get; set; } = "";
    public string? OnaylayanKullanici { get; set; }
    public DateTime? OnayTarihi { get; set; }
    public string Durum { get; set; } = "beklemede";
    
    // ⚠️ BURASI ÖNEMLİ - Property get/set kontrol et
    public List<OtTalimatSatiri> Satirlar { get; set; } = new List<OtTalimatSatiri>();
}


public class OtTalimatSatiri
{
    public int Id { get; set; }
    public int TalimatId { get; set; }
    public int FirmaId { get; set; }
    public string FirmaOdemeIsmi { get; set; } = string.Empty;
    public string FirmaIBAN { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
}