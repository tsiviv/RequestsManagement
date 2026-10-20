using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequestsManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedTo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedTo",
                table: "Requests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Backfill rows seeded before this column existed so no request is left
            // without a handler. Deterministic (by Id) to keep seed data reproducible.
            migrationBuilder.Sql("""
                UPDATE Requests
                SET AssignedTo = CHOOSE(Id % 15 + 1,
                    N'דנה כהן', N'יוסי לוי', N'מיכל אברהם', N'אבי מזרחי', N'נועה פרידמן',
                    N'רון ביטון', N'שירה גולן', N'עומר שפירא', N'תמר אזולאי', N'איתי רוזן',
                    N'הילה דהן', N'גיל אוחיון', N'ליאת ברק', N'אלון חדד', N'מאיה נחום')
                WHERE AssignedTo = N'';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedTo",
                table: "Requests");
        }
    }
}
