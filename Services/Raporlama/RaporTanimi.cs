namespace gamabelmvc.Services.Raporlama;

/// <summary>
/// Bir raporun tek yerdeki tanımı.
/// Şablon yolu: Reports/{Modul}/{Ad}.{Yon}.frx
/// </summary>
/// <param name="Modul">Reports altındaki modül klasörü.</param>
/// <param name="Ad">Şablon adı (yönlü sonek hariç).</param>
/// <param name="Tablolar">Beklenen DataTable adları (.frx ile birebir eşleşmeli).</param>
/// <param name="YetkiPolitikasi">Modülün mevcut Authorization policy adı.</param>
/// <param name="BelgeKodu">Yalnızca belge numarası kullanılıyorsa.</param>
/// <param name="VarsayilanYon">`yon` verilmezse kullanılan yön.</param>
/// <param name="DesteklenenYonler">Şablonu üretilen yönler; null ise her iki yön varsayılır.</param>
public sealed record RaporTanimi(
    string Modul,
    string Ad,
    string[] Tablolar,
    string YetkiPolitikasi,
    string? BelgeKodu = null,
    RaporYonu VarsayilanYon = RaporYonu.Dikey,
    RaporYonu[]? DesteklenenYonler = null)
{
    /// <summary>Bu tanım için şablonu bulunan yönleri döndürür.</summary>
    public RaporYonu[] Yonler() => DesteklenenYonler ?? new[] { RaporYonu.Dikey, RaporYonu.Yatay };

    public bool YonDestekliyor(RaporYonu yon) => Yonler().Contains(yon);
}

/// <summary>Projedeki rapor kayıtları. Yeni rapor = buraya tek satır.</summary>
public static class RaporKaydi
{
    /// <summary>EK MESAİ ÇİZELGESİ — 10 kolon; dikey kullanılabilir alana sığmaz, yalnızca yatay şablon üretilir.</summary>
    public static readonly RaporTanimi Mesai =
        new("Mesai", "Mesai", new[] { "Mesai" }, "PrsMenuMesai", null, RaporYonu.Yatay, new[] { RaporYonu.Yatay });

    /// <summary>PUANTAJ TAKVİMİ — 36 kolon (AdSoyad, Birim, 30 gün, X/İ/H/Hakediş); yalnızca yatay şablon üretilir.</summary>
    public static readonly RaporTanimi Puantaj =
        new("Puantaj", "Puantaj", new[] { "Puantaj" }, "PrsMenuPuantaj", null, RaporYonu.Yatay, new[] { RaporYonu.Yatay });
}
