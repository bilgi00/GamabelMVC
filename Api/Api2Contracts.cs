namespace gamabelmvc.Api;

/// <summary>Active personnel fields exposed by the read-only API2 endpoint.</summary>
public sealed record Api2PersonelResponse(
    string PerNo,
    string Ad,
    string Soyad,
    string Gorev,
    string BirimAdi);
