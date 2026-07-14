using System;

namespace gamabelmvc.Models.PRS
{
    public class SikayetModel
    {
        public int Id { get; set; }
        public DateTime Tarih { get; set; }
        public int KullaniciId { get; set; }
        public string KullaniciAdi { get; set; } // Kolay listeleme için
        public string Konu { get; set; }
        public string Detay { get; set; }
        public string Sonuc { get; set; }
        public DateTime? SonucTarihi { get; set; }
        public string Durum { get; set; } // Açık/Kapalı
    }
}