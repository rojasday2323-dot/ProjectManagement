using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManagement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiSummaryNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceAnalysisId",
                table: "ProjectDocuments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceDocumentId",
                table: "ProjectDocuments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDocuments_SourceAnalysisId",
                table: "ProjectDocuments",
                column: "SourceAnalysisId",
                unique: true,
                filter: "\"SourceAnalysisId\" IS NOT NULL AND \"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDocuments_SourceDocumentId",
                table: "ProjectDocuments",
                column: "SourceDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectDocuments_AIExtractions_SourceAnalysisId",
                table: "ProjectDocuments",
                column: "SourceAnalysisId",
                principalTable: "AIExtractions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectDocuments_ProjectDocuments_SourceDocumentId",
                table: "ProjectDocuments",
                column: "SourceDocumentId",
                principalTable: "ProjectDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectDocuments_AIExtractions_SourceAnalysisId",
                table: "ProjectDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectDocuments_ProjectDocuments_SourceDocumentId",
                table: "ProjectDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ProjectDocuments_SourceAnalysisId",
                table: "ProjectDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ProjectDocuments_SourceDocumentId",
                table: "ProjectDocuments");

            migrationBuilder.DropColumn(
                name: "SourceAnalysisId",
                table: "ProjectDocuments");

            migrationBuilder.DropColumn(
                name: "SourceDocumentId",
                table: "ProjectDocuments");
        }
    }
}
