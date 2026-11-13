using gamabelmvc.Models.StokSayim;
using Microsoft.EntityFrameworkCore;

namespace gamabelmvc.Data;

public class StokSayimDbContext(DbContextOptions<StokSayimDbContext> options) : DbContext(options)
{
    public DbSet<Sayim> Sayimlar => Set<Sayim>();
    public DbSet<SayimSistemStok> SayimSistemStoklari => Set<SayimSistemStok>();
    public DbSet<SayimHareketi> SayimHareketleri => Set<SayimHareketi>();
    public DbSet<SayimYuklemeGrubu> SayimYuklemeGruplari => Set<SayimYuklemeGrubu>();
    public DbSet<DenetimKaydi> DenetimKayitlari => Set<DenetimKaydi>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sayim>(entity =>
        {
            entity.ToTable("stok_sayimlar");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Durum).HasMaxLength(20).IsRequired();
            entity.Property(x => x.OlusturanKullanici).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Aciklama).HasColumnType("text");
            entity.HasIndex(x => x.Durum);
            entity.HasIndex(x => x.AcilisZamani);
        });

        modelBuilder.Entity<SayimSistemStok>(entity =>
        {
            entity.ToTable("stok_sayim_sistem_stok");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StokKodu).HasMaxLength(100);
            entity.Property(x => x.ReferansKodu).HasMaxLength(100);
            entity.Property(x => x.Barkod).HasMaxLength(100).UseCollation("utf8mb4_bin").IsRequired();
            entity.Property(x => x.UrunAdi).HasMaxLength(300).IsRequired();
            entity.Property(x => x.GirenMiktar).HasPrecision(18, 3);
            entity.Property(x => x.CikanMiktar).HasPrecision(18, 3);
            entity.Property(x => x.SistemStok).HasPrecision(18, 3);
            entity.Property(x => x.Birim).HasMaxLength(50);
            entity.Property(x => x.AlisFiyati).HasPrecision(18, 4);
            entity.Property(x => x.SatisFiyati).HasPrecision(18, 4);
            entity.Property(x => x.PCinsi).HasMaxLength(100);
            entity.HasIndex(x => new { x.SayimId, x.Barkod }).IsUnique();
            entity.HasIndex(x => x.Barkod);
            entity.HasOne(x => x.Sayim)
                .WithMany(x => x.SistemStoklari)
                .HasForeignKey(x => x.SayimId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SayimHareketi>(entity =>
        {
            entity.ToTable("stok_sayim_hareketleri");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Barkod).HasMaxLength(100).UseCollation("utf8mb4_bin").IsRequired();
            entity.Property(x => x.MobileRequestId).HasColumnType("char(36)");
            entity.Property(x => x.UrunAdi).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Miktar).HasPrecision(18, 3);
            entity.Property(x => x.KullaniciAdi).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Aciklama).HasColumnType("text");
            entity.Property(x => x.Kaynak).HasMaxLength(20).IsRequired();
            entity.Property(x => x.IptalEden).HasMaxLength(100);
            entity.HasIndex(x => new { x.SayimId, x.Barkod, x.Zaman });
            entity.HasIndex(x => x.Zaman);
            entity.HasIndex(x => x.YuklemeGrubuId);
            entity.HasIndex(x => x.MobileRequestId).IsUnique();
            entity.HasOne(x => x.Sayim)
                .WithMany(x => x.Hareketler)
                .HasForeignKey(x => x.SayimId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.YuklemeGrubu)
                .WithMany(x => x.Hareketler)
                .HasForeignKey(x => x.YuklemeGrubuId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SayimYuklemeGrubu>(entity =>
        {
            entity.ToTable("stok_sayim_yukleme_gruplari");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Tur).HasMaxLength(30).IsRequired();
            entity.Property(x => x.DosyaAdi).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Kullanici).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.SayimId, x.Zaman });
            entity.HasOne(x => x.Sayim)
                .WithMany(x => x.YuklemeGruplari)
                .HasForeignKey(x => x.SayimId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DenetimKaydi>(entity =>
        {
            entity.ToTable("stok_sayim_denetim_kayitlari");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kullanici).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Islem).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Detay).HasColumnType("text");
            entity.HasIndex(x => x.Zaman);
            entity.HasIndex(x => new { x.HedefId, x.Zaman });
        });
    }
}
