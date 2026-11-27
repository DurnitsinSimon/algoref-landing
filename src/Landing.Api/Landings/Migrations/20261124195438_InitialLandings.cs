using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Landing.Api.Landings.Migrations
{
    /// <inheritdoc />
    public partial class InitialLandings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "landings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    PartnerId = table.Column<string>(type: "text", nullable: false),
                    Draft = table.Column<string>(type: "jsonb", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PublishedRevision = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_landings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "landing_publications",
                columns: table => new
                {
                    LandingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_landing_publications", x => new { x.LandingId, x.Revision });
                    table.ForeignKey(
                        name: "FK_landing_publications_landings_LandingId",
                        column: x => x.LandingId,
                        principalTable: "landings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_landings_PartnerId",
                table: "landings",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "ux_landings_slug",
                table: "landings",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "landing_publications");

            migrationBuilder.DropTable(
                name: "landings");
        }
    }
}
