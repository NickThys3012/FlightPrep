using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightPrep.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class MaxLengthLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OFPSettings_AspNetUsers_UserId",
                table: "OFPSettings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OFPSettings",
                table: "OFPSettings");

            migrationBuilder.RenameTable(
                name: "OFPSettings",
                newName: "OfpSettings");

            migrationBuilder.RenameIndex(
                name: "IX_OFPSettings_UserId",
                table: "OfpSettings",
                newName: "IX_OfpSettings_UserId");

            migrationBuilder.AlterColumn<string>(
                name: "OperatorName",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_OfpSettings",
                table: "OfpSettings",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OfpSettings_AspNetUsers_UserId",
                table: "OfpSettings",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OfpSettings_AspNetUsers_UserId",
                table: "OfpSettings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OfpSettings",
                table: "OfpSettings");

            migrationBuilder.RenameTable(
                name: "OfpSettings",
                newName: "OFPSettings");

            migrationBuilder.RenameIndex(
                name: "IX_OfpSettings_UserId",
                table: "OFPSettings",
                newName: "IX_OFPSettings_UserId");

            migrationBuilder.AlterColumn<string>(
                name: "OperatorName",
                table: "AspNetUsers",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_OFPSettings",
                table: "OFPSettings",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OFPSettings_AspNetUsers_UserId",
                table: "OFPSettings",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
