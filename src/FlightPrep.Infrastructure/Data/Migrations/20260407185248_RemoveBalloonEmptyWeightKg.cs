using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightPrep.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBalloonEmptyWeightKg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmptyWeightKg",
                table: "Balloons");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "EmptyWeightKg",
                table: "Balloons",
                type: "double precision",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Balloons",
                keyColumn: "Id",
                keyValue: 1,
                column: "EmptyWeightKg",
                value: 323.39999999999998);
        }
    }
}
