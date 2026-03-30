using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FlightPrep.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWindLevelsAndFlownStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Locations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Locations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActualFlightDurationMinutes",
                table: "FlightPreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActualLandingNotes",
                table: "FlightPreparations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActualRemarks",
                table: "FlightPreparations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFlown",
                table: "FlightPreparations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SurfaceWindDirectionDeg",
                table: "FlightPreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SurfaceWindSpeedKt",
                table: "FlightPreparations",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WindLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FlightPreparationId = table.Column<int>(type: "integer", nullable: false),
                    AltitudeFt = table.Column<int>(type: "integer", nullable: false),
                    DirectionDeg = table.Column<int>(type: "integer", nullable: true),
                    SpeedKt = table.Column<int>(type: "integer", nullable: true),
                    TempC = table.Column<double>(type: "double precision", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WindLevels_FlightPreparations_FlightPreparationId",
                        column: x => x.FlightPreparationId,
                        principalTable: "FlightPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WindLevels_FlightPreparationId_Order",
                table: "WindLevels",
                columns: new[] { "FlightPreparationId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WindLevels");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ActualFlightDurationMinutes",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "ActualLandingNotes",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "ActualRemarks",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "IsFlown",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "SurfaceWindDirectionDeg",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "SurfaceWindSpeedKt",
                table: "FlightPreparations");
        }
    }
}
