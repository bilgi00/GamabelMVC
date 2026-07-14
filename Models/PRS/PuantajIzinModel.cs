namespace gamabelmvc.Models.PRS;

public class PuantajIzinModel
{
    public int Id { get; set; }
    public int PersonelId { get; set; }
    public int Yil { get; set; }
    public int Ay { get; set; }
    public int Gun { get; set; }
    public string? IzinTipi { get; set; }
    public string? Aciklama { get; set; }
}
