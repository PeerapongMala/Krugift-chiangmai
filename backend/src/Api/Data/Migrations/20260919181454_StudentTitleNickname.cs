using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StudentTitleNickname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                comment: "ชื่อเล่น · ว่างได้");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                comment: "คำนำหน้า เช่น เด็กหญิง · ว่างได้");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Students");
        }
    }
}
