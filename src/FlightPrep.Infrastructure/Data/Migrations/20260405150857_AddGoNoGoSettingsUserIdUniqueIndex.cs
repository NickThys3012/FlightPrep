using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightPrep.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoNoGoSettingsUserIdUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GoNoGoSettings_UserId",
                table: "GoNoGoSettings");

            migrationBuilder.CreateIndex(
                name: "IX_GoNoGoSettings_UserId",
                table: "GoNoGoSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GoNoGoSettings_UserId",
                table: "GoNoGoSettings");

            migrationBuilder.CreateIndex(
                name: "IX_GoNoGoSettings_UserId",
                table: "GoNoGoSettings",
                column: "UserId");
        }
    }
}
