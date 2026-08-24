using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequestsManagement.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePriorityIndexWithPriorityCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_Priority",
                table: "Requests");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_Priority_CreatedAt",
                table: "Requests",
                columns: new[] { "Priority", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_Priority_CreatedAt",
                table: "Requests");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_Priority",
                table: "Requests",
                column: "Priority");
        }
    }
}
