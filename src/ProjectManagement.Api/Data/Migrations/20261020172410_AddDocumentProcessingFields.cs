using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManagement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentProcessingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ProjectId",
                table: "ProjectDocuments",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "ProjectDocuments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "ProjectDocuments",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingStatus",
                table: "ProjectDocuments",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "Prepared");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractedText",
                table: "ProjectDocuments");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "ProjectDocuments");

            migrationBuilder.DropColumn(
                name: "ProcessingStatus",
                table: "ProjectDocuments");

            migrationBuilder.AlterColumn<int>(
                name: "ProjectId",
                table: "ProjectDocuments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
