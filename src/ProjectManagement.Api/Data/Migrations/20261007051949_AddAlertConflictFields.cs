using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManagement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertConflictFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDismissed",
                table: "ProjectAlerts");

            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "ProjectAlerts");

            migrationBuilder.AddColumn<DateTime>(
                name: "ConflictEndDate",
                table: "ProjectAlerts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConflictStartDate",
                table: "ProjectAlerts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeveloperId",
                table: "ProjectAlerts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RelatedCommitmentId",
                table: "ProjectAlerts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ProjectAlerts",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "Open");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ProjectAlerts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAlerts_DeveloperId",
                table: "ProjectAlerts",
                column: "DeveloperId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAlerts_RelatedCommitmentId",
                table: "ProjectAlerts",
                column: "RelatedCommitmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectAlerts_Commitments_RelatedCommitmentId",
                table: "ProjectAlerts",
                column: "RelatedCommitmentId",
                principalTable: "Commitments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectAlerts_Developers_DeveloperId",
                table: "ProjectAlerts",
                column: "DeveloperId",
                principalTable: "Developers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectAlerts_Commitments_RelatedCommitmentId",
                table: "ProjectAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectAlerts_Developers_DeveloperId",
                table: "ProjectAlerts");

            migrationBuilder.DropIndex(
                name: "IX_ProjectAlerts_DeveloperId",
                table: "ProjectAlerts");

            migrationBuilder.DropIndex(
                name: "IX_ProjectAlerts_RelatedCommitmentId",
                table: "ProjectAlerts");

            migrationBuilder.DropColumn(
                name: "ConflictEndDate",
                table: "ProjectAlerts");

            migrationBuilder.DropColumn(
                name: "ConflictStartDate",
                table: "ProjectAlerts");

            migrationBuilder.DropColumn(
                name: "DeveloperId",
                table: "ProjectAlerts");

            migrationBuilder.DropColumn(
                name: "RelatedCommitmentId",
                table: "ProjectAlerts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ProjectAlerts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ProjectAlerts");

            migrationBuilder.AddColumn<bool>(
                name: "IsDismissed",
                table: "ProjectAlerts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "ProjectAlerts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
