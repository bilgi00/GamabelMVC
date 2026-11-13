using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gamabelmvc.Data.Migrations
{
    /// <inheritdoc />
    public partial class StokSayimBaslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "stok_sayim_denetim_kayitlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Zaman = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Kullanici = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Islem = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HedefId = table.Column<int>(type: "int", nullable: true),
                    Detay = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stok_sayim_denetim_kayitlari", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "stok_sayimlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Ad = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Durum = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AcilisZamani = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    KapanisZamani = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    OlusturanKullanici = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Aciklama = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stok_sayimlar", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "stok_sayim_sistem_stok",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SayimId = table.Column<int>(type: "int", nullable: false),
                    Barkod = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UrunAdi = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SistemStok = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stok_sayim_sistem_stok", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stok_sayim_sistem_stok_stok_sayimlar_SayimId",
                        column: x => x.SayimId,
                        principalTable: "stok_sayimlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "stok_sayim_yukleme_gruplari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SayimId = table.Column<int>(type: "int", nullable: false),
                    Tur = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DosyaAdi = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kullanici = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Zaman = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SatirSayisi = table.Column<int>(type: "int", nullable: false),
                    HataliSatirSayisi = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stok_sayim_yukleme_gruplari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stok_sayim_yukleme_gruplari_stok_sayimlar_SayimId",
                        column: x => x.SayimId,
                        principalTable: "stok_sayimlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "stok_sayim_hareketleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SayimId = table.Column<int>(type: "int", nullable: false),
                    Barkod = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UrunAdi = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Miktar = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    KullaniciAdi = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Zaman = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Aciklama = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kaynak = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    YuklemeGrubuId = table.Column<int>(type: "int", nullable: true),
                    YeniUrunMu = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Iptal = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IptalEden = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IptalZamani = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stok_sayim_hareketleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stok_sayim_hareketleri_stok_sayim_yukleme_gruplari_YuklemeGr~",
                        column: x => x.YuklemeGrubuId,
                        principalTable: "stok_sayim_yukleme_gruplari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stok_sayim_hareketleri_stok_sayimlar_SayimId",
                        column: x => x.SayimId,
                        principalTable: "stok_sayimlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_stok_sayim_denetim_kayitlari_HedefId_Zaman",
                table: "stok_sayim_denetim_kayitlari",
                columns: new[] { "HedefId", "Zaman" });

            migrationBuilder.CreateIndex(
                name: "IX_stok_sayim_denetim_kayitlari_Zaman",
                table: "stok_sayim_denetim_kayitlari",
                column: "Zaman");

            migrationBuilder.CreateIndex(
                name: "IX_stok_sayim_hareketleri_SayimId_Barkod_Zaman",
                table: "stok_sayim_hareketleri",
                columns: new[] { "SayimId", "Barkod", "Zaman" });

            migrationBuilder.CreateIndex(
                name: "IX_stok_sayim_hareketleri_YuklemeGrubuId",
                table: "stok_sayim_hareketleri",
                column: "YuklemeGrubuId");

            migrationBuilder.CreateIndex(
                name: "IX_stok_sayim_hareketleri_Zaman",
                table: "stok_sayim_hareketleri",
                column: "Zaman");

            migrationBuilder.CreateIndex(
                name: "IX_stok_sayim_sistem_stok_Barkod",
                table: "stok_sayim_sistem_stok",
                column: "Barkod");

            migrationBuilder.CreateIndex(
                name: "IX_stok_sayim_sistem_stok_SayimId_Barkod",
                table: "stok_sayim_sistem_stok",
                columns: new[] { "SayimId", "Barkod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stok_sayim_yukleme_gruplari_SayimId_Zaman",
                table: "stok_sayim_yukleme_gruplari",
                columns: new[] { "SayimId", "Zaman" });

            migrationBuilder.CreateIndex(
                name: "IX_stok_sayimlar_AcilisZamani",
                table: "stok_sayimlar",
                column: "AcilisZamani");

            migrationBuilder.CreateIndex(
                name: "IX_stok_sayimlar_Durum",
                table: "stok_sayimlar",
                column: "Durum");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stok_sayim_denetim_kayitlari");

            migrationBuilder.DropTable(
                name: "stok_sayim_hareketleri");

            migrationBuilder.DropTable(
                name: "stok_sayim_sistem_stok");

            migrationBuilder.DropTable(
                name: "stok_sayim_yukleme_gruplari");

            migrationBuilder.DropTable(
                name: "stok_sayimlar");
        }
    }
}
