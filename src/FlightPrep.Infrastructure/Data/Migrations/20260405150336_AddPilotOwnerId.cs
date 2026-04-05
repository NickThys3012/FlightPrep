using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightPrep.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPilotOwnerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Pilots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "GoNoGoSettings",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pilots_OwnerId",
                table: "Pilots",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_GoNoGoSettings_UserId",
                table: "GoNoGoSettings",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoNoGoSettings_AspNetUsers_UserId",
                table: "GoNoGoSettings",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Pilots_AspNetUsers_OwnerId",
                table: "Pilots",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoNoGoSettings_AspNetUsers_UserId",
                table: "GoNoGoSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_Pilots_AspNetUsers_OwnerId",
                table: "Pilots");

            migrationBuilder.DropIndex(
                name: "IX_Pilots_OwnerId",
                table: "Pilots");

            migrationBuilder.DropIndex(
                name: "IX_GoNoGoSettings_UserId",
                table: "GoNoGoSettings");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Pilots");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "GoNoGoSettings");
        }
    }
}
