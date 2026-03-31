using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightPrep.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrajectorySimulationJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrajectorySimulationJson",
                table: "FlightPreparations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrajectorySimulationJson",
                table: "FlightPreparations");
        }
    }
}
