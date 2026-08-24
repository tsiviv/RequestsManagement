using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequestsManagement.Migrations
{
    /// <inheritdoc />
    public partial class RestrictSortableFieldsToStatusPriorityDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_OrganizationName",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_Title",
                table: "Requests");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_UpdatedAt",
                table: "Requests",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_UpdatedAt",
                table: "Requests");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_OrganizationName",
                table: "Requests",
                column: "OrganizationName");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_Title",
                table: "Requests",
                column: "Title");
        }
    }
}
