using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace gamabelmvc.Models.PRS;

public class OtFirma
{
    public int Id { get; set; }
    public string CariIsmi { get; set; } = string.Empty;
    public string OdemeIsmi { get; set; } = string.Empty;
    public string IBAN { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? EmailCc { get; set; }
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

public class ExcelYuklemeOzetViewModel
{
    public int ExcelSatirSayisi { get; set; }
    public int CiftIslemSayisi { get; set; }
    public int OdenmisIslemSayisi { get; set; }
    public int MevcutAcikIslemSayisi { get; set; }
    public int Eklenen { get; set; }
    public List<string> CiftFaturalar { get; set; } = new();
    public List<string> OdenmisFaturalar { get; set; } = new();
    public int EkOdenmisFaturaSayisi { get; set; }
}



public class OtFaturaViewModel
{
    public int Id { get; set; }
    public string CariKart { get; set; } = string.Empty;
    public string FaturaNo { get; set; } = string.Empty;
    public DateTime? FaturaTarihi { get; set; }  // ✅ YENİ
    public decimal Bakiye { get; set; }
    public string OdemeDurumu { get; set; } = "bekliyor";
    public int ImportBatchId { get; set; }
    public string? OdemeIsmi { get; set; }
    public string? IBAN { get; set; }
}


public class OtAcikFatura
{
    public int Id { get; set; }
    public int ImportBatchId { get; set; }
    public string CariKart { get; set; } = string.Empty;
    public string FaturaNo { get; set; } = string.Empty;
    public DateTime? FaturaTarihi { get; set; }  // ✅ YENİ
    public decimal Bakiye { get; set; }
    public bool OdemeyeDahilEdildi { get; set; }
    public string OdemeDurumu { get; set; } = "bekliyor";
    public DateTime OlusturmaTarihi { get; set; }
    public DateTime GuncellemeTarihi { get; set; }
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
    public List<int> AcikFaturaIdleri { get; set; } = new();
}

public class ManuelTalimatModel
{
    [Required(ErrorMessage = "Talimat tarihi zorunludur.")]
    public DateTime? TalimatTarihi { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir banka seçin.")]
    public int BankaId { get; set; }

    [MinLength(1, ErrorMessage = "En az bir fatura satırı ekleyin.")]
    public List<ManuelTalimatSatiriModel> Satirlar { get; set; } = new() { new() };
}

public class ManuelTalimatSatiriModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Her satır için bir firma seçin.")]
    public int FirmaId { get; set; }

    [Required(ErrorMessage = "Fatura adı zorunludur.")]
    [StringLength(200, ErrorMessage = "Fatura adı en fazla 200 karakter olabilir.")]
    public string FaturaAdi { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tutar zorunludur.")]
    [RegularExpression(@"^\d{1,16}([.,]\d{1,2})?$", ErrorMessage = "Tutar en fazla iki ondalık basamak içerebilir.")]
    public string Tutar { get; set; } = string.Empty;
}