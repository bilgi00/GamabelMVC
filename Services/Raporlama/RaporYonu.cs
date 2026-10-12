namespace gamabelmvc.Services.Raporlama;

public enum RaporYonu
{
    Dikey,
    Yatay
}

/// <summary>
/// `yon` parametresi sıkı doğrulanır: yalnızca "dikey" veya "yatay" kabul edilir,
/// başka değerde false döner (sessizce varsayılana düşülmez).
/// </summary>
public static class RaporYon
{
    public static bool TryCoz(string? yon, out RaporYonu sonuc)
    {
        switch (yon?.Trim().ToLowerInvariant())
        {
            case "dikey": sonuc = RaporYonu.Dikey; return true;
            case "yatay": sonuc = RaporYonu.Yatay; return true;
            default:      sonuc = default;         return false;
        }
    }

    public static string Ad(this RaporYonu yon) => yon == RaporYonu.Dikey ? "Dikey" : "Yatay";
}
