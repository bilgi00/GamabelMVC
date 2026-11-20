using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gamabelmvc.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkSayimHareketToSistemStok : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SayimSistemStokId",
                table: "stok_sayim_hareketleri",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE stok_sayim_hareketleri AS hareket " +
                "INNER JOIN stok_sayim_sistem_stok AS stok " +
                "ON stok.SayimId = hareket.SayimId AND stok.Barkod = hareket.Barkod " +
                "SET hareket.SayimSistemStokId = stok.Id " +
                "WHERE hareket.SayimSistemStokId IS NULL AND hareket.Barkod <> '';");

            migrationBuilder.CreateIndex(
                name: "IX_stok_sayim_hareketleri_SayimSistemStokId",
                table: "stok_sayim_hareketleri",
                column: "SayimSistemStokId");

            migrationBuilder.AddForeignKey(
                name: "FK_stok_sayim_hareketleri_stok_sayim_sistem_stok_SayimSistemSto~",
                table: "stok_sayim_hareketleri",
                column: "SayimSistemStokId",
                principalTable: "stok_sayim_sistem_stok",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stok_sayim_hareketleri_stok_sayim_sistem_stok_SayimSistemSto~",
                table: "stok_sayim_hareketleri");

            migrationBuilder.DropIndex(
                name: "IX_stok_sayim_hareketleri_SayimSistemStokId",
                table: "stok_sayim_hareketleri");

            migrationBuilder.DropColumn(
                name: "SayimSistemStokId",
                table: "stok_sayim_hareketleri");
        }
    }
}
