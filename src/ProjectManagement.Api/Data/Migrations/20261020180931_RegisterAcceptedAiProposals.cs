using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManagement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegisterAcceptedAiProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIProposalRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AIExtractionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProposalId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIProposalRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIProposalRegistrations_AIExtractions_AIExtractionId",
                        column: x => x.AIExtractionId,
                        principalTable: "AIExtractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AIProposalRegistrations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIProposalRegistrations_AIExtractionId_ProposalId",
                table: "AIProposalRegistrations",
                columns: new[] { "AIExtractionId", "ProposalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AIProposalRegistrations_ProjectId",
                table: "AIProposalRegistrations",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIProposalRegistrations");
        }
    }
}
