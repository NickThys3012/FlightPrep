using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightPrep.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixOFPSettingsCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OFPSettings_AspNetUsers_UserId",
                table: "OFPSettings");

            migrationBuilder.AddForeignKey(
                name: "FK_OFPSettings_AspNetUsers_UserId",
                table: "OFPSettings",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OFPSettings_AspNetUsers_UserId",
                table: "OFPSettings");

            migrationBuilder.AddForeignKey(
                name: "FK_OFPSettings_AspNetUsers_UserId",
                table: "OFPSettings",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
