using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FlightPrep.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOFPFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsChild",
                table: "Passengers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTransport",
                table: "Passengers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsAssistance",
                table: "Passengers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "CylindersWeightKg",
                table: "FlightPreparations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FuelAvailableMinutes",
                table: "FlightPreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FuelConsumptionL",
                table: "FlightPreparations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FuelRequiredMinutes",
                table: "FlightPreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LandingLocationText",
                table: "FlightPreparations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OFPBasketWeightKg",
                table: "FlightPreparations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OFPBurnerWeightKg",
                table: "FlightPreparations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OFPEnvelopeWeightKg",
                table: "FlightPreparations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatorName",
                table: "FlightPreparations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PicWeightKg",
                table: "FlightPreparations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "PlannedLandingTime",
                table: "FlightPreparations",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VisibleDefects",
                table: "FlightPreparations",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisibleDefectsNotes",
                table: "FlightPreparations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BasketWeightKg",
                table: "Balloons",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BurnerWeightKg",
                table: "Balloons",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EnvelopeOnlyWeightKg",
                table: "Balloons",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatorName",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WeightKg",
                table: "AspNetUsers",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OFPSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    PassengerEquipmentWeightKg = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OFPSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OFPSettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.UpdateData(
                table: "Balloons",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BasketWeightKg", "BurnerWeightKg", "EnvelopeOnlyWeightKg" },
                values: new object[] { null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_OFPSettings_UserId",
                table: "OFPSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OFPSettings");

            migrationBuilder.DropColumn(
                name: "IsChild",
                table: "Passengers");

            migrationBuilder.DropColumn(
                name: "IsTransport",
                table: "Passengers");

            migrationBuilder.DropColumn(
                name: "NeedsAssistance",
                table: "Passengers");

            migrationBuilder.DropColumn(
                name: "CylindersWeightKg",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "FuelAvailableMinutes",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "FuelConsumptionL",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "FuelRequiredMinutes",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "LandingLocationText",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "OFPBasketWeightKg",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "OFPBurnerWeightKg",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "OFPEnvelopeWeightKg",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "OperatorName",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "PicWeightKg",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "PlannedLandingTime",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "VisibleDefects",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "VisibleDefectsNotes",
                table: "FlightPreparations");

            migrationBuilder.DropColumn(
                name: "BasketWeightKg",
                table: "Balloons");

            migrationBuilder.DropColumn(
                name: "BurnerWeightKg",
                table: "Balloons");

            migrationBuilder.DropColumn(
                name: "EnvelopeOnlyWeightKg",
                table: "Balloons");

            migrationBuilder.DropColumn(
                name: "OperatorName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                table: "AspNetUsers");
        }
    }
}
