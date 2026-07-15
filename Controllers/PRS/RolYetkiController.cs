using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace gamabelmvc.Controllers.PRS;

public class RolYetkiController : Controller
{
    private readonly string _connectionString;

    public RolYetkiController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MyConnection")!;
    }

    public async Task<IActionResult> Index()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return RedirectToAction("Login", "Account");

        if (HttpContext.Session.GetString("Rol") != "admin")
            return RedirectToAction("Index", "Home");

        var roller = new List<RollModel>();

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new MySqlCommand("SELECT * FROM roll ORDER BY ad", connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            roller.Add(new RollModel
            {
                Id = reader.GetInt32("id"),
                Ad = reader.GetString("ad"),
                Aciklama = reader.IsDBNull(reader.GetOrdinal("aciklama")) ? string.Empty : reader.GetString("aciklama"),
                AktifMi = reader.GetBoolean("aktif_mi"),
                Menu_KullaniciYonetimi = reader.GetBoolean("menu_kullanici_yonetimi"),
                Menu_Personel = reader.GetBoolean("menu_personel"),
                Menu_Puantaj = reader.GetBoolean("menu_puantaj"),
                Menu_Rapor = reader.GetBoolean("menu_rapor"),
                Menu_Mesai = reader.GetBoolean("menu_mesai"),
                Menu_Tatiller = reader.GetBoolean("menu_tatiller"),
                Menu_OdemeTalimat = reader.GetBoolean("menu_odeme_talimat"),
                Menu_FirmaYonetimi = reader.GetBoolean("menu_firma_yonetimi"),
                Menu_BankaYonetimi = reader.GetBoolean("menu_banka_yonetimi"),
                Menu_Yetkilendirme = reader.GetBoolean("menu_yetkilendirme"),
                Menu_Dokumantasyon = reader.GetBoolean("menu_dokumantasyon"),
                Menu_SikayetAdmin = reader.GetBoolean("menu_sikayet_admin")
            });
        }

        return View(roller);
    }

    [HttpPost]
    public async Task<IActionResult> Guncelle(int id, string ad, string aciklama, bool aktifMi,
        bool menuKullaniciYonetimi, bool menuPersonel, bool menuPuantaj, bool menuRapor,
        bool menuMesai, bool menuTatiller, bool menuOdemeTalimat, bool menuFirmaYonetimi,
        bool menuBankaYonetimi, bool menuYetkilendirme, bool menuDokumantasyon, bool menuSikayetAdmin)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("KullaniciAdi")))
            return RedirectToAction("Login", "Account");

        if (HttpContext.Session.GetString("Rol") != "admin")
            return RedirectToAction("Index", "Home");

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new MySqlCommand(@"
            UPDATE roll
            SET ad = @Ad,
                aciklama = @Aciklama,
                aktif_mi = @AktifMi,
                menu_kullanici_yonetimi = @Menu_KullaniciYonetimi,
                menu_personel = @Menu_Personel,
                menu_puantaj = @Menu_Puantaj,
                menu_rapor = @Menu_Rapor,
                menu_mesai = @Menu_Mesai,
                menu_tatiller = @Menu_Tatiller,
                menu_odeme_talimat = @Menu_OdemeTalimat,
                menu_firma_yonetimi = @Menu_FirmaYonetimi,
                menu_banka_yonetimi = @Menu_BankaYonetimi,
                menu_yetkilendirme = @Menu_Yetkilendirme,
                menu_dokumantasyon = @Menu_Dokumantasyon,
                menu_sikayet_admin = @Menu_SikayetAdmin
            WHERE id = @Id", connection);

        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Ad", ad);
        command.Parameters.AddWithValue("@Aciklama", aciklama ?? string.Empty);
        command.Parameters.AddWithValue("@AktifMi", aktifMi);
        command.Parameters.AddWithValue("@Menu_KullaniciYonetimi", menuKullaniciYonetimi);
        command.Parameters.AddWithValue("@Menu_Personel", menuPersonel);
        command.Parameters.AddWithValue("@Menu_Puantaj", menuPuantaj);
        command.Parameters.AddWithValue("@Menu_Rapor", menuRapor);
        command.Parameters.AddWithValue("@Menu_Mesai", menuMesai);
        command.Parameters.AddWithValue("@Menu_Tatiller", menuTatiller);
        command.Parameters.AddWithValue("@Menu_OdemeTalimat", menuOdemeTalimat);
        command.Parameters.AddWithValue("@Menu_FirmaYonetimi", menuFirmaYonetimi);
        command.Parameters.AddWithValue("@Menu_BankaYonetimi", menuBankaYonetimi);
        command.Parameters.AddWithValue("@Menu_Yetkilendirme", menuYetkilendirme);
        command.Parameters.AddWithValue("@Menu_Dokumantasyon", menuDokumantasyon);
        command.Parameters.AddWithValue("@Menu_SikayetAdmin", menuSikayetAdmin);
        await command.ExecuteNonQueryAsync();

        return RedirectToAction("Index");
    }
}

public class RollModel
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public bool AktifMi { get; set; }
    public bool Menu_KullaniciYonetimi { get; set; }
    public bool Menu_Personel { get; set; }
    public bool Menu_Puantaj { get; set; }
    public bool Menu_Rapor { get; set; }
    public bool Menu_Mesai { get; set; }
    public bool Menu_Tatiller { get; set; }
    public bool Menu_OdemeTalimat { get; set; }
    public bool Menu_FirmaYonetimi { get; set; }
    public bool Menu_BankaYonetimi { get; set; }
    public bool Menu_Yetkilendirme { get; set; }
    public bool Menu_Dokumantasyon { get; set; }
    public bool Menu_SikayetAdmin { get; set; }
}
