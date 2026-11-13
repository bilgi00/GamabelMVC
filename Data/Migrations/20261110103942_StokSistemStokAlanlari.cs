using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gamabelmvc.Data.Migrations
{
    /// <inheritdoc />
    public partial class StokSistemStokAlanlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AlisFiyati",
                table: "stok_sayim_sistem_stok",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Birim",
                table: "stok_sayim_sistem_stok",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "CikanMiktar",
                table: "stok_sayim_sistem_stok",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GirenMiktar",
                table: "stok_sayim_sistem_stok",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PCinsi",
                table: "stok_sayim_sistem_stok",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ReferansKodu",
                table: "stok_sayim_sistem_stok",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "SatisFiyati",
                table: "stok_sayim_sistem_stok",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StokKodu",
                table: "stok_sayim_sistem_stok",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlisFiyati",
                table: "stok_sayim_sistem_stok");

            migrationBuilder.DropColumn(
                name: "Birim",
                table: "stok_sayim_sistem_stok");

            migrationBuilder.DropColumn(
                name: "CikanMiktar",
                table: "stok_sayim_sistem_stok");

            migrationBuilder.DropColumn(
                name: "GirenMiktar",
                table: "stok_sayim_sistem_stok");

            migrationBuilder.DropColumn(
                name: "PCinsi",
                table: "stok_sayim_sistem_stok");

            migrationBuilder.DropColumn(
                name: "ReferansKodu",
                table: "stok_sayim_sistem_stok");

            migrationBuilder.DropColumn(
                name: "SatisFiyati",
                table: "stok_sayim_sistem_stok");

            migrationBuilder.DropColumn(
                name: "StokKodu",
                table: "stok_sayim_sistem_stok");
        }
    }
}
