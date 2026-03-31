using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightPrep.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVeldEigenaarGemeld : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "VeldEigenaarGemeld",
                table: "FlightPreparations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VeldEigenaarGemeld",
                table: "FlightPreparations");
        }
    }
}
