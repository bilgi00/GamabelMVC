using Microsoft.AspNetCore.Mvc;
using gamabelmvc.Models.PRS;
using System;
using System.Collections.Generic;
using System.Data;
using MySqlConnector;
using Microsoft.AspNetCore.Http;

namespace gamabelmvc.Controllers.PRS
{
    using gamabelmvc.Services;

    public class SikayetController : Controller
    {
        private readonly DbConnectionFactory _dbFactory;
        public SikayetController(DbConnectionFactory dbFactory)
        {
            _dbFactory = dbFactory;
        }

        // Şikayet listesi (herkes görebilir)
        public IActionResult Index()
        {
            var sikayetler = new List<SikayetModel>();
            using (var conn = _dbFactory.CreateConnectionAsync().Result)
            {
                var cmd = new MySqlCommand("SELECT * FROM stk_Sikayet ORDER BY Tarih DESC", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        sikayetler.Add(new SikayetModel
                        {
                            Id = reader.GetInt32("Id"),
                            Tarih = reader.GetDateTime("Tarih"),
                            KullaniciId = reader.GetInt32("KullaniciId"),
                            KullaniciAdi = reader.GetString("KullaniciAdi"),
                            Konu = reader.GetString("Konu"),
                            Detay = reader["Detay"]?.ToString(),
                            Sonuc = reader["Sonuc"]?.ToString(),
                            SonucTarihi = reader["SonucTarihi"] == DBNull.Value ? null : reader.GetDateTime("SonucTarihi"),
                            Durum = reader.GetString("Durum")
                        });
                    }
                }
            }
            return View(sikayetler);
        }

        // Şikayet oluşturma (herkes)
        [HttpGet]
        public IActionResult Ekle()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Ekle(string konu, string detay)
        {
            var kullaniciId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var kullaniciAdi = HttpContext.Session.GetString("KullaniciAdi") ?? "Anonim";
            using (var conn = _dbFactory.CreateConnectionAsync().Result)
            {
                var cmd = new MySqlCommand("INSERT INTO stk_Sikayet (Tarih, KullaniciId, KullaniciAdi, Konu, Detay, Durum) VALUES (NOW(), @KullaniciId, @KullaniciAdi, @Konu, @Detay, 'Açık')", conn);
                cmd.Parameters.AddWithValue("@KullaniciId", kullaniciId);
                cmd.Parameters.AddWithValue("@KullaniciAdi", kullaniciAdi);
                cmd.Parameters.AddWithValue("@Konu", konu);
                cmd.Parameters.AddWithValue("@Detay", detay ?? "");
                cmd.ExecuteNonQuery();
            }
            TempData["Success"] = "Şikayetiniz kaydedildi. Yöneticiye bildirim gönderildi.";
            TempData["NotifyAdmin"] = true;
            return RedirectToAction("Index");
        }

        // Admin: Şikayetleri yönet
        public IActionResult Admin()
        {
            // Admin yetkisi kontrolü eklenmeli
            var sikayetler = new List<SikayetModel>();
            using (var conn = _dbFactory.CreateConnectionAsync().Result)
            {
                var cmd = new MySqlCommand("SELECT * FROM stk_Sikayet ORDER BY Durum, Tarih DESC", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        sikayetler.Add(new SikayetModel
                        {
                            Id = reader.GetInt32("Id"),
                            Tarih = reader.GetDateTime("Tarih"),
                            KullaniciId = reader.GetInt32("KullaniciId"),
                            KullaniciAdi = reader.GetString("KullaniciAdi"),
                            Konu = reader.GetString("Konu"),
                            Detay = reader["Detay"]?.ToString(),
                            Sonuc = reader["Sonuc"]?.ToString(),
                            SonucTarihi = reader["SonucTarihi"] == DBNull.Value ? null : reader.GetDateTime("SonucTarihi"),
                            Durum = reader.GetString("Durum")
                        });
                    }
                }
            }
            return View(sikayetler);
        }

        // Admin: Sonuç kaydet
        [HttpPost]
        public IActionResult SonucKaydet(int id, string sonuc, string durum)
        {
            // Admin yetkisi kontrolü eklenmeli
            using (var conn = _dbFactory.CreateConnectionAsync().Result)
            {
                var cmd = new MySqlCommand("UPDATE stk_Sikayet SET Sonuc=@Sonuc, SonucTarihi=NOW(), Durum=@Durum WHERE Id=@Id", conn);
                cmd.Parameters.AddWithValue("@Sonuc", sonuc);
                cmd.Parameters.AddWithValue("@Durum", string.IsNullOrWhiteSpace(durum) ? "Kapalı" : durum);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
            TempData["Success"] = "Sonuç kaydedildi. Kullanıcıya bildirim gönderildi.";
            TempData["NotifyUser"] = id;
            return RedirectToAction("Admin");
        }
    }
}