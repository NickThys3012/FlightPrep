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
            // PostgreSQL cannot cast text → jsonb automatically; USING clause is required.
            migrationBuilder.Sql(
                """ALTER TABLE "FlightPreparations" ALTER COLUMN "TrajectorySimulationJson" TYPE jsonb USING "TrajectorySimulationJson"::jsonb;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """ALTER TABLE "FlightPreparations" ALTER COLUMN "TrajectorySimulationJson" TYPE text USING "TrajectorySimulationJson"::text;""");
        }
    }
}
