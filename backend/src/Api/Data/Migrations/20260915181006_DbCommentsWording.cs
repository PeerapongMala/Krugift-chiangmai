using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DbCommentsWording : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "Appeals",
                comment: "คำถามเรื่องคะแนนจากนักเรียน (หน้าเว็บเรียกว่า สอบถามคะแนน) · มีคำถามที่ยังไม่เสร็จสิ้นได้ครั้งละหนึ่งคำถามต่อรายการ",
                oldComment: "เรื่องท้วงคะแนน · นักเรียนมีเรื่องที่ยังไม่ปิดได้ครั้งละหนึ่งเรื่องต่อรายการ");

            migrationBuilder.AlterTable(
                name: "AppealMessages",
                comment: "ข้อความในคำถามเรื่องคะแนน (ทั้งคำถามของนักเรียนและคำตอบของครู)",
                oldComment: "ข้อความในเรื่องท้วงคะแนน");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Teachers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "สิทธิ์: Owner = ผู้ดูแลระบบ (เห็นข้อมูลครูทุกคน + เพิ่ม/ลบครู) · Teacher = ครู (เห็นเฉพาะภาคเรียนของตัวเอง)",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "ยศ: Owner = เห็นข้อมูลครูทุกคน + จัดการรายชื่อครู · Teacher = เห็นเฉพาะภาคเรียนของตัวเอง");

            migrationBuilder.AlterColumn<string>(
                name: "GoogleSub",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "บัญชี Google ที่นักเรียนเชื่อมไว้ (Google subject id) · null = ยังไม่เคยเข้าสู่ระบบ",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "บัญชี Google ที่นักเรียนผูกไว้ (Google subject id) · null = ยังไม่เคยเข้าระบบ");

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "Appeals",
                type: "integer",
                nullable: false,
                comment: "นักเรียนที่ถาม (Students.Id)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "นักเรียนที่ท้วง (Students.Id)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Appeals",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "สถานะ: Open = รอครูตอบ · Answered = ครูตอบแล้ว · Closed = เสร็จสิ้น",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "สถานะ: Open = รอครูตอบ · Answered = ครูตอบแล้ว · Closed = ปิดแล้ว");

            migrationBuilder.AlterColumn<int>(
                name: "ItemId",
                table: "Appeals",
                type: "integer",
                nullable: false,
                comment: "รายการคะแนนที่ถาม (Items.Id)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "รายการคะแนนที่ท้วง (Items.Id)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Appeals",
                type: "timestamp with time zone",
                nullable: false,
                comment: "เวลาที่ส่งคำถาม (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldComment: "เวลาที่เปิดเรื่อง (UTC)");

            migrationBuilder.AlterColumn<int>(
                name: "AppealId",
                table: "AppealMessages",
                type: "integer",
                nullable: false,
                comment: "คำถาม (Appeals.Id)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "เรื่องท้วง (Appeals.Id)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "Appeals",
                comment: "เรื่องท้วงคะแนน · นักเรียนมีเรื่องที่ยังไม่ปิดได้ครั้งละหนึ่งเรื่องต่อรายการ",
                oldComment: "คำถามเรื่องคะแนนจากนักเรียน (หน้าเว็บเรียกว่า สอบถามคะแนน) · มีคำถามที่ยังไม่เสร็จสิ้นได้ครั้งละหนึ่งคำถามต่อรายการ");

            migrationBuilder.AlterTable(
                name: "AppealMessages",
                comment: "ข้อความในเรื่องท้วงคะแนน",
                oldComment: "ข้อความในคำถามเรื่องคะแนน (ทั้งคำถามของนักเรียนและคำตอบของครู)");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Teachers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "ยศ: Owner = เห็นข้อมูลครูทุกคน + จัดการรายชื่อครู · Teacher = เห็นเฉพาะภาคเรียนของตัวเอง",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "สิทธิ์: Owner = ผู้ดูแลระบบ (เห็นข้อมูลครูทุกคน + เพิ่ม/ลบครู) · Teacher = ครู (เห็นเฉพาะภาคเรียนของตัวเอง)");

            migrationBuilder.AlterColumn<string>(
                name: "GoogleSub",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "บัญชี Google ที่นักเรียนผูกไว้ (Google subject id) · null = ยังไม่เคยเข้าระบบ",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "บัญชี Google ที่นักเรียนเชื่อมไว้ (Google subject id) · null = ยังไม่เคยเข้าสู่ระบบ");

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "Appeals",
                type: "integer",
                nullable: false,
                comment: "นักเรียนที่ท้วง (Students.Id)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "นักเรียนที่ถาม (Students.Id)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Appeals",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "สถานะ: Open = รอครูตอบ · Answered = ครูตอบแล้ว · Closed = ปิดแล้ว",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "สถานะ: Open = รอครูตอบ · Answered = ครูตอบแล้ว · Closed = เสร็จสิ้น");

            migrationBuilder.AlterColumn<int>(
                name: "ItemId",
                table: "Appeals",
                type: "integer",
                nullable: false,
                comment: "รายการคะแนนที่ท้วง (Items.Id)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "รายการคะแนนที่ถาม (Items.Id)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Appeals",
                type: "timestamp with time zone",
                nullable: false,
                comment: "เวลาที่เปิดเรื่อง (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldComment: "เวลาที่ส่งคำถาม (UTC)");

            migrationBuilder.AlterColumn<int>(
                name: "AppealId",
                table: "AppealMessages",
                type: "integer",
                nullable: false,
                comment: "เรื่องท้วง (Appeals.Id)",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "คำถาม (Appeals.Id)");
        }
    }
}
