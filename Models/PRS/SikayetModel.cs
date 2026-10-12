using System;

namespace gamabelmvc.Models.PRS
{
    public class SikayetModel
    {
        public int Id { get; set; }
        public DateTime Tarih { get; set; }
        public int KullaniciId { get; set; }
        public string KullaniciAdi { get; set; } = string.Empty; // Kolay listeleme için
        public string Konu { get; set; } = string.Empty;
        public string? Detay { get; set; }
        public string? Sonuc { get; set; }
        public DateTime? SonucTarihi { get; set; }
        public string Durum { get; set; } = string.Empty; // Açık/Kapalı
    }
}