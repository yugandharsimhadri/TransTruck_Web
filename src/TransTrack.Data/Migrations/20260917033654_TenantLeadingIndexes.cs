using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransTrack.Data.Migrations
{
    /// <inheritdoc />
    public partial class TenantLeadingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_States_Name",
                table: "States");

            migrationBuilder.DropIndex(
                name: "IX_Parties_Name",
                table: "Parties");

            migrationBuilder.DropIndex(
                name: "IX_Owners_Name",
                table: "Owners");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceCategories_Name",
                table: "MaintenanceCategories");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseCategories_Name",
                table: "ExpenseCategories");

            migrationBuilder.DropIndex(
                name: "IX_Cities_Name",
                table: "Cities");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ChangedOn",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_States_CompanyId_Name",
                table: "States",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Parties_CompanyId_Name",
                table: "Parties",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Owners_CompanyId_Name",
                table: "Owners",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCategories_CompanyId_Name",
                table: "MaintenanceCategories",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_CompanyId_Name",
                table: "ExpenseCategories",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Cities_CompanyId_Name",
                table: "Cities",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CompanyId_ChangedOn",
                table: "AuditLogs",
                columns: new[] { "CompanyId", "ChangedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_States_CompanyId_Name",
                table: "States");

            migrationBuilder.DropIndex(
                name: "IX_Parties_CompanyId_Name",
                table: "Parties");

            migrationBuilder.DropIndex(
                name: "IX_Owners_CompanyId_Name",
                table: "Owners");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceCategories_CompanyId_Name",
                table: "MaintenanceCategories");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseCategories_CompanyId_Name",
                table: "ExpenseCategories");

            migrationBuilder.DropIndex(
                name: "IX_Cities_CompanyId_Name",
                table: "Cities");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_CompanyId_ChangedOn",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_States_Name",
                table: "States",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_Name",
                table: "Parties",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Owners_Name",
                table: "Owners",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCategories_Name",
                table: "MaintenanceCategories",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_Name",
                table: "ExpenseCategories",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_Name",
                table: "Cities",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ChangedOn",
                table: "AuditLogs",
                column: "ChangedOn");
        }
    }
}
