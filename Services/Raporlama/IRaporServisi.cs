using FastReport.Web;

namespace gamabelmvc.Services.Raporlama;

public sealed record RaporCikti(byte[] Icerik, string SablonSurum);

public enum RaporCiktiTuru
{
    Pdf,
    /// <summary>.frx şablonundan üretilen tek dosyalık HTML (gömülü görsellerle).</summary>
    Html
}

public interface IRaporServisi
{
    WebReport Onizleme(RaporTanimi tanim, RaporYonu yon, System.Data.DataSet veri, IDictionary<string, object?> prm);
    RaporCikti Pdf(RaporTanimi tanim, RaporYonu yon, System.Data.DataSet veri, IDictionary<string, object?> prm);

    /// <summary>İstenen türde çıktı üretir (Pdf veya Html). Excel için kullanmayın — Excel .frx'ten üretilmez (standart Bölüm 12).</summary>
    RaporCikti Cikar(RaporTanimi tanim, RaporCiktiTuru tur, RaporYonu yon, System.Data.DataSet veri, IDictionary<string, object?> prm);
}
