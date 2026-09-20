using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ItemSourceKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                table: "Items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                comment: "ชื่อตามหัวตารางในไฟล์ครู ใช้จับคู่ตอนนำเข้าซ้ำ ครูจึงเปลี่ยนชื่อที่แสดงได้โดยไม่เกิดรายการซ้ำ · ว่าง = สร้างในเว็บเอง");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "Items");
        }
    }
}
