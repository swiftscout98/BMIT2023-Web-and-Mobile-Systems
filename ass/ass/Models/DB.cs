using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ass.Models;

public class DB : DbContext
{
    public DB(DbContextOptions options) : base(options) { }
    // DbSets
    public DbSet<Student> Students { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<Class> Classes { get; set; }
    public DbSet<Schedule> Schedules { get; set; }
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<Tutor> Tutors { get; set; }
    public DbSet<Admin> Admins { get; set; }
}

//Entity Class
#nullable disable warnings
public class Student
{
    [Key, MaxLength(10)]
    public string StudentId { get; set; }

    [MaxLength(50)]
    public string StudentName { get; set; }

    [MaxLength(50)]
    public string StudentEmail { get; set; }

    [MaxLength(256)]
    public string StudentPassword { get; set; }

    [MaxLength(15)]
    public string StudentPhone { get; set; }

    [MaxLength(100)]
    public string StudentAvatarURL { get; set; }

    [MaxLength(1)]
    public string StudentGender { get; set; }

    [MaxLength(10)]
    public string VerifyStatus { get; set; }

    //Navigation
    public List<Class> Classes { get; set; } = [];
    public List<Attendance> Attendances { get; set; } = [];

}


public class Attendance
{
    [Key, MaxLength(10)]
    public string AttendanceId { get; set; }

    [MaxLength(50)]
    public string IsPresent { get; set; }

    [MaxLength(50)]
    public string Status { get; set; }

    [Column(TypeName = "DATETIME")]
    public DateTime Date { get; set; }

    //FK
    [ForeignKey("Student")]
    public string StudentId { get; set; }
    [ForeignKey("Schedule")]
    public string ScheduleId { get; set; }

    //Navigation Properties
    public Student Student { get; set; }
    public Schedule Schedule { get; set; }


}

public class Class
{
    [Key, MaxLength(10)]
    public string ClassId { get; set; }

    [MaxLength(50)]
    public string ClassName { get; set; }

    [MaxLength(255)]
    public string ClassDescription { get; set; }
    [MaxLength(50)]
    public string Status { get; set; }

    //Navigation
    public List<Student> Students { get; set; } = [];

}

public class Schedule
{
    [Key, MaxLength(10)]
    public string ScheduleId { get; set; }

    [Column(TypeName = "DATETIME")]
    public DateTime Date { get; set; }

    [Column(TypeName = "TIME")]
    public TimeSpan Time { get; set; }

    [Column(TypeName = "TIME")]
    public TimeSpan StartTime { get; set; }

    [Column(TypeName = "TIME")]
    public TimeSpan EndTime { get; set; }

    [MaxLength(6)]
    public string AttendanceCode { get; set; }  // Add this field

    //FK
    [ForeignKey("Class")]
    public string ClassId { get; set; }

    //Navigation
    public Class Class { get; set; }
    public List<Attendance> Attendances { get; set; } = [];
    public List<Subject> Subjects { get; set; } = [];

}

public class Subject
{
    [Key, MaxLength(10)]
    public string SubjectId { get; set; }

    [MaxLength(100)]
    public string SubjectName { get; set; }

    [MaxLength(100)]
    public string SubjectDescription { get; set; }

    // Navigation property
    public List<Tutor> Tutors { get; set; } = [];
    public List<Schedule> Schedules { get; set; } = [];
}


public class Tutor
{
    [Key, MaxLength(10)]
    public string TutorId { get; set; }

    [MaxLength(100)]
    public string TutorName { get; set; }

    [MaxLength(50)]
    public string TutorEmail { get; set; }

    [MaxLength(256)]
    public string TutorPassword { get; set; }

    [MaxLength(15)]
    public string TutorPhone { get; set; }
    [MaxLength(1)]
    public string TutorGender { get; set; }

    [MaxLength(100)]
    public string TutorAvatarURL { get; set; }

    public List<Subject> Subjects { get; set; } = [];

}


public class Admin
{
    [Key, MaxLength(10)]
    public string AdminId { get; set; }

    [MaxLength(100)]
    public string AdminName { get; set; }

    [MaxLength(256)]
    public string AdminPassword { get; set; }

    [MaxLength(100)]
    public string AdminAvatarURL { get; set; }
}

