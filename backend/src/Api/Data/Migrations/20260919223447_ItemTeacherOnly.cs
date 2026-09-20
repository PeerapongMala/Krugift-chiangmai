using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ItemTeacherOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TeacherOnly",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "true = เห็นเฉพาะครู นักเรียนไม่เห็นและสอบถามไม่ได้ (เช่น คะแนนดิบก่อนคิดเป็นคะแนนเก็บ)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeacherOnly",
                table: "Items");
        }
    }
}
