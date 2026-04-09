using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightPrep.Data.Migrations
{
    /// <inheritdoc />
    public partial class UseJsonbForTrajectorySimulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TrajectorySimulationJson",
                table: "FlightPreparations",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TrajectorySimulationJson",
                table: "FlightPreparations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);
        }
    }
}
