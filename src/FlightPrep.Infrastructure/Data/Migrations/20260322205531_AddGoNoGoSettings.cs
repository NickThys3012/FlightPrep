using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FlightPrep.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoNoGoSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoNoGoSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WindYellowKt = table.Column<double>(type: "double precision", nullable: false),
                    WindRedKt = table.Column<double>(type: "double precision", nullable: false),
                    VisYellowKm = table.Column<double>(type: "double precision", nullable: false),
                    VisRedKm = table.Column<double>(type: "double precision", nullable: false),
                    CapeYellowJkg = table.Column<double>(type: "double precision", nullable: false),
                    CapeRedJkg = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoNoGoSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoNoGoSettings");
        }
    }
}
