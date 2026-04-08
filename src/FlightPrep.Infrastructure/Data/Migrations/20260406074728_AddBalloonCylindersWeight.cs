using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightPrep.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBalloonCylindersWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CylindersWeightKg",
                table: "Balloons",
                type: "double precision",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Balloons",
                keyColumn: "Id",
                keyValue: 1,
                column: "CylindersWeightKg",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CylindersWeightKg",
                table: "Balloons");
        }
    }
}
