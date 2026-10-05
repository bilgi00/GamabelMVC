using System.ComponentModel.DataAnnotations;

namespace gamabelmvc.Models.PRS;

public sealed class BirimYonetimViewModel
{
    public List<BirimYonetimSatiri> Birimler { get; set; } = new();

    [Required(ErrorMessage = "Birim adı zorunludur.")]
    [StringLength(255, ErrorMessage = "Birim adı en fazla 255 karakter olabilir.")]
    public string YeniBirimAdi { get; set; } = string.Empty;
}

public sealed class BirimYonetimSatiri
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Birim adı zorunludur.")]
    [StringLength(255, ErrorMessage = "Birim adı en fazla 255 karakter olabilir.")]
    public string BirimAdi { get; set; } = string.Empty;
}

public sealed class PersonelYonetimViewModel
{
    public string Arama { get; set; } = string.Empty;
    public List<string> Birimler { get; set; } = new();
    public List<PersonelYonetimSatiri> Personeller { get; set; } = new();
}

public sealed class PersonelYonetimSatiri
{
    [Required]
    [StringLength(50)]
    public string PerNo { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Ad { get; set; }

    [StringLength(100)]
    public string? Soyad { get; set; }

    [StringLength(255)]
    public string? BirimAdi { get; set; }

    [StringLength(100)]
    public string? PerStatu { get; set; }
}
