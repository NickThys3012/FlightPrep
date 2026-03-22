using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FlightPrep.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Balloons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Registration = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Volume = table.Column<string>(type: "text", nullable: false),
                    EmptyWeightKg = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Balloons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IcaoCode = table.Column<string>(type: "text", nullable: true),
                    AirspaceNotes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pilots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    WeightKg = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pilots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlightPreparations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Datum = table.Column<DateOnly>(type: "date", nullable: false),
                    Tijdstip = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    BalloonId = table.Column<int>(type: "integer", nullable: true),
                    PilotId = table.Column<int>(type: "integer", nullable: true),
                    LocationId = table.Column<int>(type: "integer", nullable: true),
                    Metar = table.Column<string>(type: "text", nullable: true),
                    Taf = table.Column<string>(type: "text", nullable: true),
                    WindPerHoogte = table.Column<string>(type: "text", nullable: true),
                    Neerslag = table.Column<string>(type: "text", nullable: true),
                    TemperatuurC = table.Column<double>(type: "double precision", nullable: true),
                    DauwpuntC = table.Column<double>(type: "double precision", nullable: true),
                    QnhHpa = table.Column<double>(type: "double precision", nullable: true),
                    ZichtbaarheidKm = table.Column<double>(type: "double precision", nullable: true),
                    CapeJkg = table.Column<double>(type: "double precision", nullable: true),
                    NotamsGecontroleerd = table.Column<string>(type: "text", nullable: false),
                    Luchtruimstructuur = table.Column<string>(type: "text", nullable: true),
                    Beperkingen = table.Column<string>(type: "text", nullable: true),
                    Obstakels = table.Column<string>(type: "text", nullable: true),
                    EhboEnBlusser = table.Column<string>(type: "text", nullable: false),
                    PassagierslijstIngevuld = table.Column<string>(type: "text", nullable: false),
                    VluchtplanIngediend = table.Column<string>(type: "text", nullable: false),
                    BranderGetest = table.Column<bool>(type: "boolean", nullable: false),
                    GasflaconsGecontroleerd = table.Column<bool>(type: "boolean", nullable: false),
                    BallonVisueel = table.Column<bool>(type: "boolean", nullable: false),
                    VerankeringenGecontroleerd = table.Column<bool>(type: "boolean", nullable: false),
                    InstrumentenWerkend = table.Column<bool>(type: "boolean", nullable: false),
                    PaxBriefing = table.Column<string>(type: "text", nullable: true),
                    EnvelopeWeightKg = table.Column<double>(type: "double precision", nullable: true),
                    MaxAltitudeFt = table.Column<int>(type: "integer", nullable: true),
                    LiftUnits = table.Column<double>(type: "double precision", nullable: true),
                    TotaalLiftKg = table.Column<double>(type: "double precision", nullable: true),
                    LoadNotes = table.Column<string>(type: "text", nullable: true),
                    Traject = table.Column<string>(type: "text", nullable: true),
                    Ballonbulletin = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightPreparations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlightPreparations_Balloons_BalloonId",
                        column: x => x.BalloonId,
                        principalTable: "Balloons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FlightPreparations_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FlightPreparations_Pilots_PilotId",
                        column: x => x.PilotId,
                        principalTable: "Pilots",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Passengers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FlightPreparationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    WeightKg = table.Column<double>(type: "double precision", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Passengers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Passengers_FlightPreparations_FlightPreparationId",
                        column: x => x.FlightPreparationId,
                        principalTable: "FlightPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Balloons",
                columns: new[] { "Id", "EmptyWeightKg", "Registration", "Type", "Volume" },
                values: new object[] { 1, 323.39999999999998, "OO-BUT", "BB22N", "2200M³" });

            migrationBuilder.CreateIndex(
                name: "IX_FlightPreparations_BalloonId",
                table: "FlightPreparations",
                column: "BalloonId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightPreparations_LocationId",
                table: "FlightPreparations",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightPreparations_PilotId",
                table: "FlightPreparations",
                column: "PilotId");

            migrationBuilder.CreateIndex(
                name: "IX_Passengers_FlightPreparationId_Order",
                table: "Passengers",
                columns: new[] { "FlightPreparationId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Passengers");

            migrationBuilder.DropTable(
                name: "FlightPreparations");

            migrationBuilder.DropTable(
                name: "Balloons");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Pilots");
        }
    }
}
