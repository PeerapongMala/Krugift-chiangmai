using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DbComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "Terms",
                comment: "ภาคเรียน เช่น 1/2569 · เป็นของครูหนึ่งคน");

            migrationBuilder.AlterTable(
                name: "Teachers",
                comment: "ครูที่เข้าระบบได้ด้วย Google · ครูคนแรกมาจาก env TEACHER_EMAILS หลังจากนั้นเพิ่ม/ลบที่หน้า จัดการครู");

            migrationBuilder.AlterTable(
                name: "Students",
                comment: "นักเรียนทั้งโรงเรียน · หนึ่งคนมีแถวเดียวใช้ข้ามทุกภาคเรียน (อยู่ห้องไหนดูที่ Enrollments)");

            migrationBuilder.AlterTable(
                name: "Scores",
                comment: "คะแนนของนักเรียนต่อรายการ · แก้ผ่านแอปเท่านั้น เพื่อให้มีประวัติใน ScoreAudits");

            migrationBuilder.AlterTable(
                name: "ScoreAudits",
                comment: "ประวัติการแก้คะแนนทุกครั้ง · ตั้งใจไม่มี FK ประวัติจะอยู่ต่อแม้ลบรายการหรือนักเรียนไปแล้ว");

            migrationBuilder.AlterTable(
                name: "Items",
                comment: "รายการที่ให้คะแนนในห้อง เช่น สอบกลางภาค เต็ม 20");

            migrationBuilder.AlterTable(
                name: "Enrollments",
                comment: "ตารางเชื่อมห้องกับนักเรียน: นักเรียนคนไหนอยู่ห้องไหน และเลขที่เท่าไหร่");

            migrationBuilder.AlterTable(
                name: "Classrooms",
                comment: "ห้องเรียนในภาคเรียน เช่น ม.2/1 · นักเรียนในห้องดูที่ Enrollments");

            migrationBuilder.AlterTable(
                name: "Appeals",
                comment: "เรื่องท้วงคะแนน · นักเรียนมีเรื่องที่ยังไม่ปิดได้ครั้งละหนึ่งเรื่องต่อรายการ");

            migrationBuilder.AlterTable(
                name: "AppealMessages",
                comment: "ข้อความในเรื่องท้วงคะแนน");

            migrationBuilder.AlterColumn<int>(
                name: "TeacherId",
                table: "Terms",
                type: "integer",
                nullable: false,
                comment: "ครูเจ้าของภาคเรียน (Teachers.Id)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<bool>(
                name: "PublicScores",
                table: "Terms",
                type: "boolean",
                nullable: false,
                comment: "เปิดให้ดูคะแนนด่วนโดยไม่ต้องล็อกอิน (ห้อง + เลขที่ + รหัสนักเรียน) · true = เปิด",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Terms",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                comment: "ชื่อภาคเรียน เช่น 1/2569 · ครูคนเดียวกันห้ามซ้ำ",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Terms",
                type: "timestamp with time zone",
                nullable: false,
                comment: "เวลาที่สร้าง (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Terms",
                type: "integer",
                nullable: false,
                comment: "รหัสภายในระบบ",
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Teachers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "ยศ: Owner = เห็นข้อมูลครูทุกคน + จัดการรายชื่อครู · Teacher = เห็นเฉพาะภาคเรียนของตัวเอง",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Teachers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                comment: "ชื่อที่แสดง · ว่างได้ ถ้าว่างระบบแสดงอีเมลแทน",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Teachers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                comment: "อีเมล Google ที่ใช้ล็อกอิน (ตัวพิมพ์เล็ก ห้ามซ้ำ)",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Teachers",
                type: "integer",
                nullable: false,
                comment: "รหัสภายในระบบ",
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<string>(
                name: "StudentCode",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                comment: "รหัสนักเรียนของโรงเรียน · ห้ามซ้ำ",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                comment: "นามสกุล",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

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
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                comment: "ชื่อ",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Students",
                type: "integer",
                nullable: false,
                comment: "รหัสภายในระบบ (ไม่ใช่รหัสนักเรียน)",
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "Scores",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true,
                comment: "คะแนนที่ได้ · null = ยังไม่ได้กรอก · ห้ามเกินคะแนนเต็ม (ตรวจในแอป)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)",
                oldPrecision: 6,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "Scores",
                type: "integer",
                nullable: false,
                comment: "นักเรียน (Students.Id)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "ItemId",
                table: "Scores",
                type: "integer",
                nullable: false,
                comment: "รายการคะแนน (Items.Id)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "TeacherId",
                table: "ScoreAudits",
                type: "integer",
                nullable: false,
                comment: "ครูที่แก้ (Teachers.Id · อาจถูกลบไปแล้ว)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "ScoreAudits",
                type: "integer",
                nullable: false,
                comment: "นักเรียนที่ถูกแก้คะแนน (Students.Id)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "OldValue",
                table: "ScoreAudits",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true,
                comment: "คะแนนก่อนแก้ · null = ว่าง",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)",
                oldPrecision: 6,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "NewValue",
                table: "ScoreAudits",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true,
                comment: "คะแนนหลังแก้ · null = ล้างเป็นว่าง",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)",
                oldPrecision: 6,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ItemId",
                table: "ScoreAudits",
                type: "integer",
                nullable: false,
                comment: "รายการที่ถูกแก้ (Items.Id · อาจถูกลบไปแล้ว)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<DateTime>(
                name: "At",
                table: "ScoreAudits",
                type: "timestamp with time zone",
                nullable: false,
                comment: "เวลาที่แก้ (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "ScoreAudits",
                type: "bigint",
                nullable: false,
                comment: "รหัสภายในระบบ",
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "SortOrder",
                table: "Items",
                type: "integer",
                nullable: false,
                comment: "ลำดับการแสดง · น้อยขึ้นก่อน",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                comment: "ชื่อรายการ · ในห้องเดียวกันห้ามซ้ำ",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<decimal>(
                name: "MaxScore",
                table: "Items",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                comment: "คะแนนเต็ม (มากกว่า 0)",
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)",
                oldPrecision: 6,
                oldScale: 2);

            migrationBuilder.AlterColumn<int>(
                name: "ClassroomId",
                table: "Items",
                type: "integer",
                nullable: false,
                comment: "ห้องเรียน (Classrooms.Id)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Items",
                type: "integer",
                nullable: false,
                comment: "รหัสภายในระบบ",
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "No",
                table: "Enrollments",
                type: "integer",
                nullable: false,
                comment: "เลขที่ในห้อง (1-999)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "Enrollments",
                type: "integer",
                nullable: false,
                comment: "นักเรียน (Students.Id)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "ClassroomId",
                table: "Enrollments",
                type: "integer",
                nullable: false,
                comment: "ห้องเรียน (Classrooms.Id)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "TermId",
                table: "Classrooms",
                type: "integer",
                nullable: false,
                comment: "ภาคเรียนที่ห้องนี้อยู่ (Terms.Id)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Classrooms",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                comment: "ชื่อห้อง เช่น ม.2/1 · ในภาคเรียนเดียวกันห้ามซ้ำ",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Classrooms",
                type: "integer",
                nullable: false,
                comment: "รหัสภายในระบบ",
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<bool>(
                name: "UnreadByTeacher",
                table: "Appeals",
                type: "boolean",
                nullable: false,
                comment: "ครูยังไม่ได้อ่านข้อความล่าสุด (ใช้แสดง badge ฝั่งครู)",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "UnreadByStudent",
                table: "Appeals",
                type: "boolean",
                nullable: false,
                comment: "นักเรียนยังไม่ได้อ่านข้อความล่าสุด (ใช้แสดง badge ฝั่งนักเรียน)",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "Appeals",
                type: "integer",
                nullable: false,
                comment: "นักเรียนที่ท้วง (Students.Id)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Appeals",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "สถานะ: Open = รอครูตอบ · Answered = ครูตอบแล้ว · Closed = ปิดแล้ว",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "ItemId",
                table: "Appeals",
                type: "integer",
                nullable: false,
                comment: "รายการคะแนนที่ท้วง (Items.Id)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Appeals",
                type: "timestamp with time zone",
                nullable: false,
                comment: "เวลาที่เปิดเรื่อง (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Appeals",
                type: "integer",
                nullable: false,
                comment: "รหัสภายในระบบ",
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<bool>(
                name: "FromTeacher",
                table: "AppealMessages",
                type: "boolean",
                nullable: false,
                comment: "true = ครูเขียน · false = นักเรียนเขียน",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                table: "AppealMessages",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                comment: "ข้อความ (ไม่เกิน 2000 ตัวอักษร)",
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<DateTime>(
                name: "At",
                table: "AppealMessages",
                type: "timestamp with time zone",
                nullable: false,
                comment: "เวลาที่ส่ง (UTC)",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<int>(
                name: "AppealId",
                table: "AppealMessages",
                type: "integer",
                nullable: false,
                comment: "เรื่องท้วง (Appeals.Id)",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "AppealMessages",
                type: "integer",
                nullable: false,
                comment: "รหัสภายในระบบ",
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "Terms",
                oldComment: "ภาคเรียน เช่น 1/2569 · เป็นของครูหนึ่งคน");

            migrationBuilder.AlterTable(
                name: "Teachers",
                oldComment: "ครูที่เข้าระบบได้ด้วย Google · ครูคนแรกมาจาก env TEACHER_EMAILS หลังจากนั้นเพิ่ม/ลบที่หน้า จัดการครู");

            migrationBuilder.AlterTable(
                name: "Students",
                oldComment: "นักเรียนทั้งโรงเรียน · หนึ่งคนมีแถวเดียวใช้ข้ามทุกภาคเรียน (อยู่ห้องไหนดูที่ Enrollments)");

            migrationBuilder.AlterTable(
                name: "Scores",
                oldComment: "คะแนนของนักเรียนต่อรายการ · แก้ผ่านแอปเท่านั้น เพื่อให้มีประวัติใน ScoreAudits");

            migrationBuilder.AlterTable(
                name: "ScoreAudits",
                oldComment: "ประวัติการแก้คะแนนทุกครั้ง · ตั้งใจไม่มี FK ประวัติจะอยู่ต่อแม้ลบรายการหรือนักเรียนไปแล้ว");

            migrationBuilder.AlterTable(
                name: "Items",
                oldComment: "รายการที่ให้คะแนนในห้อง เช่น สอบกลางภาค เต็ม 20");

            migrationBuilder.AlterTable(
                name: "Enrollments",
                oldComment: "ตารางเชื่อมห้องกับนักเรียน: นักเรียนคนไหนอยู่ห้องไหน และเลขที่เท่าไหร่");

            migrationBuilder.AlterTable(
                name: "Classrooms",
                oldComment: "ห้องเรียนในภาคเรียน เช่น ม.2/1 · นักเรียนในห้องดูที่ Enrollments");

            migrationBuilder.AlterTable(
                name: "Appeals",
                oldComment: "เรื่องท้วงคะแนน · นักเรียนมีเรื่องที่ยังไม่ปิดได้ครั้งละหนึ่งเรื่องต่อรายการ");

            migrationBuilder.AlterTable(
                name: "AppealMessages",
                oldComment: "ข้อความในเรื่องท้วงคะแนน");

            migrationBuilder.AlterColumn<int>(
                name: "TeacherId",
                table: "Terms",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "ครูเจ้าของภาคเรียน (Teachers.Id)");

            migrationBuilder.AlterColumn<bool>(
                name: "PublicScores",
                table: "Terms",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "เปิดให้ดูคะแนนด่วนโดยไม่ต้องล็อกอิน (ห้อง + เลขที่ + รหัสนักเรียน) · true = เปิด");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Terms",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComment: "ชื่อภาคเรียน เช่น 1/2569 · ครูคนเดียวกันห้ามซ้ำ");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Terms",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldComment: "เวลาที่สร้าง (UTC)");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Terms",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "รหัสภายในระบบ")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Teachers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "ยศ: Owner = เห็นข้อมูลครูทุกคน + จัดการรายชื่อครู · Teacher = เห็นเฉพาะภาคเรียนของตัวเอง");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Teachers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComment: "ชื่อที่แสดง · ว่างได้ ถ้าว่างระบบแสดงอีเมลแทน");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Teachers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComment: "อีเมล Google ที่ใช้ล็อกอิน (ตัวพิมพ์เล็ก ห้ามซ้ำ)");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Teachers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "รหัสภายในระบบ")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<string>(
                name: "StudentCode",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComment: "รหัสนักเรียนของโรงเรียน · ห้ามซ้ำ");

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComment: "นามสกุล");

            migrationBuilder.AlterColumn<string>(
                name: "GoogleSub",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "บัญชี Google ที่นักเรียนผูกไว้ (Google subject id) · null = ยังไม่เคยเข้าระบบ");

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComment: "ชื่อ");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Students",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "รหัสภายในระบบ (ไม่ใช่รหัสนักเรียน)")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "Scores",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)",
                oldPrecision: 6,
                oldScale: 2,
                oldNullable: true,
                oldComment: "คะแนนที่ได้ · null = ยังไม่ได้กรอก · ห้ามเกินคะแนนเต็ม (ตรวจในแอป)");

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "Scores",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "นักเรียน (Students.Id)");

            migrationBuilder.AlterColumn<int>(
                name: "ItemId",
                table: "Scores",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "รายการคะแนน (Items.Id)");

            migrationBuilder.AlterColumn<int>(
                name: "TeacherId",
                table: "ScoreAudits",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "ครูที่แก้ (Teachers.Id · อาจถูกลบไปแล้ว)");

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "ScoreAudits",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "นักเรียนที่ถูกแก้คะแนน (Students.Id)");

            migrationBuilder.AlterColumn<decimal>(
                name: "OldValue",
                table: "ScoreAudits",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)",
                oldPrecision: 6,
                oldScale: 2,
                oldNullable: true,
                oldComment: "คะแนนก่อนแก้ · null = ว่าง");

            migrationBuilder.AlterColumn<decimal>(
                name: "NewValue",
                table: "ScoreAudits",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)",
                oldPrecision: 6,
                oldScale: 2,
                oldNullable: true,
                oldComment: "คะแนนหลังแก้ · null = ล้างเป็นว่าง");

            migrationBuilder.AlterColumn<int>(
                name: "ItemId",
                table: "ScoreAudits",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "รายการที่ถูกแก้ (Items.Id · อาจถูกลบไปแล้ว)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "At",
                table: "ScoreAudits",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldComment: "เวลาที่แก้ (UTC)");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "ScoreAudits",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "รหัสภายในระบบ")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "SortOrder",
                table: "Items",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "ลำดับการแสดง · น้อยขึ้นก่อน");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComment: "ชื่อรายการ · ในห้องเดียวกันห้ามซ้ำ");

            migrationBuilder.AlterColumn<decimal>(
                name: "MaxScore",
                table: "Items",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)",
                oldPrecision: 6,
                oldScale: 2,
                oldComment: "คะแนนเต็ม (มากกว่า 0)");

            migrationBuilder.AlterColumn<int>(
                name: "ClassroomId",
                table: "Items",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "ห้องเรียน (Classrooms.Id)");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Items",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "รหัสภายในระบบ")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "No",
                table: "Enrollments",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "เลขที่ในห้อง (1-999)");

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "Enrollments",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "นักเรียน (Students.Id)");

            migrationBuilder.AlterColumn<int>(
                name: "ClassroomId",
                table: "Enrollments",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "ห้องเรียน (Classrooms.Id)");

            migrationBuilder.AlterColumn<int>(
                name: "TermId",
                table: "Classrooms",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "ภาคเรียนที่ห้องนี้อยู่ (Terms.Id)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Classrooms",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComment: "ชื่อห้อง เช่น ม.2/1 · ในภาคเรียนเดียวกันห้ามซ้ำ");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Classrooms",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "รหัสภายในระบบ")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<bool>(
                name: "UnreadByTeacher",
                table: "Appeals",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "ครูยังไม่ได้อ่านข้อความล่าสุด (ใช้แสดง badge ฝั่งครู)");

            migrationBuilder.AlterColumn<bool>(
                name: "UnreadByStudent",
                table: "Appeals",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "นักเรียนยังไม่ได้อ่านข้อความล่าสุด (ใช้แสดง badge ฝั่งนักเรียน)");

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "Appeals",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "นักเรียนที่ท้วง (Students.Id)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Appeals",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "สถานะ: Open = รอครูตอบ · Answered = ครูตอบแล้ว · Closed = ปิดแล้ว");

            migrationBuilder.AlterColumn<int>(
                name: "ItemId",
                table: "Appeals",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "รายการคะแนนที่ท้วง (Items.Id)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Appeals",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldComment: "เวลาที่เปิดเรื่อง (UTC)");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Appeals",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "รหัสภายในระบบ")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<bool>(
                name: "FromTeacher",
                table: "AppealMessages",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "true = ครูเขียน · false = นักเรียนเขียน");

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                table: "AppealMessages",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldComment: "ข้อความ (ไม่เกิน 2000 ตัวอักษร)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "At",
                table: "AppealMessages",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldComment: "เวลาที่ส่ง (UTC)");

            migrationBuilder.AlterColumn<int>(
                name: "AppealId",
                table: "AppealMessages",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "เรื่องท้วง (Appeals.Id)");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "AppealMessages",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "รหัสภายในระบบ")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }
    }
}
