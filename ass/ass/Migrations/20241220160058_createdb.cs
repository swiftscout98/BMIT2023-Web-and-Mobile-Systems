using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ass.Migrations
{
    /// <inheritdoc />
    public partial class createdb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Admins",
                columns: table => new
                {
                    AdminId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AdminName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AdminPassword = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AdminAvatarURL = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Admins", x => x.AdminId);
                });

            migrationBuilder.CreateTable(
                name: "Classes",
                columns: table => new
                {
                    ClassId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ClassName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClassDescription = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classes", x => x.ClassId);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    StudentId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    StudentName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StudentEmail = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StudentPassword = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StudentPhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    StudentAvatarURL = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StudentGender = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    VerifyStatus = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.StudentId);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    SubjectId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SubjectName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubjectDescription = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.SubjectId);
                });

            migrationBuilder.CreateTable(
                name: "Tutors",
                columns: table => new
                {
                    TutorId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TutorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TutorEmail = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TutorPassword = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TutorPhone = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    TutorGender = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    TutorAvatarURL = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tutors", x => x.TutorId);
                });

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    ScheduleId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Date = table.Column<DateTime>(type: "DATETIME", nullable: false),
                    Time = table.Column<TimeSpan>(type: "TIME", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "TIME", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "TIME", nullable: false),
                    AttendanceCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    ClassId = table.Column<string>(type: "nvarchar(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.ScheduleId);
                    table.ForeignKey(
                        name: "FK_Schedules_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "ClassId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassStudent",
                columns: table => new
                {
                    ClassesClassId = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    StudentsStudentId = table.Column<string>(type: "nvarchar(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassStudent", x => new { x.ClassesClassId, x.StudentsStudentId });
                    table.ForeignKey(
                        name: "FK_ClassStudent_Classes_ClassesClassId",
                        column: x => x.ClassesClassId,
                        principalTable: "Classes",
                        principalColumn: "ClassId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassStudent_Students_StudentsStudentId",
                        column: x => x.StudentsStudentId,
                        principalTable: "Students",
                        principalColumn: "StudentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubjectTutor",
                columns: table => new
                {
                    SubjectsSubjectId = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    TutorsTutorId = table.Column<string>(type: "nvarchar(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectTutor", x => new { x.SubjectsSubjectId, x.TutorsTutorId });
                    table.ForeignKey(
                        name: "FK_SubjectTutor_Subjects_SubjectsSubjectId",
                        column: x => x.SubjectsSubjectId,
                        principalTable: "Subjects",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubjectTutor_Tutors_TutorsTutorId",
                        column: x => x.TutorsTutorId,
                        principalTable: "Tutors",
                        principalColumn: "TutorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    AttendanceId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsPresent = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateTime>(type: "DATETIME", nullable: false),
                    StudentId = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    ScheduleId = table.Column<string>(type: "nvarchar(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendances", x => x.AttendanceId);
                    table.ForeignKey(
                        name: "FK_Attendances_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "ScheduleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Attendances_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "StudentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleSubject",
                columns: table => new
                {
                    SchedulesScheduleId = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    SubjectsSubjectId = table.Column<string>(type: "nvarchar(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleSubject", x => new { x.SchedulesScheduleId, x.SubjectsSubjectId });
                    table.ForeignKey(
                        name: "FK_ScheduleSubject_Schedules_SchedulesScheduleId",
                        column: x => x.SchedulesScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "ScheduleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleSubject_Subjects_SubjectsSubjectId",
                        column: x => x.SubjectsSubjectId,
                        principalTable: "Subjects",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_ScheduleId",
                table: "Attendances",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_StudentId",
                table: "Attendances",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassStudent_StudentsStudentId",
                table: "ClassStudent",
                column: "StudentsStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_ClassId",
                table: "Schedules",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSubject_SubjectsSubjectId",
                table: "ScheduleSubject",
                column: "SubjectsSubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectTutor_TutorsTutorId",
                table: "SubjectTutor",
                column: "TutorsTutorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Admins");

            migrationBuilder.DropTable(
                name: "Attendances");

            migrationBuilder.DropTable(
                name: "ClassStudent");

            migrationBuilder.DropTable(
                name: "ScheduleSubject");

            migrationBuilder.DropTable(
                name: "SubjectTutor");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "Schedules");

            migrationBuilder.DropTable(
                name: "Subjects");

            migrationBuilder.DropTable(
                name: "Tutors");

            migrationBuilder.DropTable(
                name: "Classes");
        }
    }
}
