using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightPrep.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceBalloonVolumeWithDouble : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Volume",
                table: "Balloons");

            migrationBuilder.AddColumn<double>(
                name: "ElevationM",
                table: "Locations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "InternalEnvelopeTempC",
                table: "Balloons",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VolumeM3",
                table: "Balloons",
                type: "double precision",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Balloons",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "InternalEnvelopeTempC", "VolumeM3" },
                values: new object[] { 80.0, 2200.0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ElevationM",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "InternalEnvelopeTempC",
                table: "Balloons");

            migrationBuilder.DropColumn(
                name: "VolumeM3",
                table: "Balloons");

            migrationBuilder.AddColumn<string>(
                name: "Volume",
                table: "Balloons",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Balloons",
                keyColumn: "Id",
                keyValue: 1,
                column: "Volume",
                value: "2200M³");
        }
    }
}
