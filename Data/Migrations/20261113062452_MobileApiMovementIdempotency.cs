using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gamabelmvc.Data.Migrations
{
    /// <inheritdoc />
    public partial class MobileApiMovementIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MobileRequestId",
                table: "stok_sayim_hareketleri",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_stok_sayim_hareketleri_MobileRequestId",
                table: "stok_sayim_hareketleri",
                column: "MobileRequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stok_sayim_hareketleri_MobileRequestId",
                table: "stok_sayim_hareketleri");

            migrationBuilder.DropColumn(
                name: "MobileRequestId",
                table: "stok_sayim_hareketleri");
        }
    }
}
