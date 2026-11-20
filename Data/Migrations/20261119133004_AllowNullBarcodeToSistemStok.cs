using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gamabelmvc.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowNullBarcodeToSistemStok : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Barkod",
                table: "stok_sayim_sistem_stok",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "utf8mb4_bin",
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldCollation: "utf8mb4_bin")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(
                "UPDATE stok_sayim_sistem_stok SET Barkod = NULL WHERE Barkod = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE stok_sayim_sistem_stok SET Barkod = '' WHERE Barkod IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "Barkod",
                table: "stok_sayim_sistem_stok",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                collation: "utf8mb4_bin",
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldCollation: "utf8mb4_bin")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
