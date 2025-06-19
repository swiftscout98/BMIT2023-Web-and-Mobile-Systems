using System.ComponentModel.DataAnnotations;

namespace ass.Models;

#nullable disable warnings

// ------------------------------------------------------------------------
// Students VM
// ------------------------------------------------------------------------
public class StudentVM
{
    [Required(ErrorMessage = "Student ID is required")]
    [MaxLength(10)]
    public string StudentId { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [MaxLength(50)]
    public string StudentName { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress]
    [MaxLength(50)]
    public string StudentEmail { get; set; }

    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be atleast 8 characters long.")]
    [MaxLength(50)]
    public string StudentPassword { get; set; }

    [Required(ErrorMessage = "Phone number is required")]
    [Phone]
    [MaxLength(15, ErrorMessage = "Phone number has exceeded the limit.")]
    [MinLength(11, ErrorMessage = "Phone number must be atleast 11 numbers long")]
    public string StudentPhone { get; set; }

    [Required(ErrorMessage = "Please select a photo")]
    public IFormFile StudentAvatarURL { get; set; }

    [Required(ErrorMessage = "Please select gender")]
    [MaxLength(1)]
    public string StudentGender { get; set; }

    public string VerifyStatus { get; set; }
}

public class StudentUpdateVM
{
    public string StudentId { get; set; }
    public string StudentName { get; set; }
    public string StudentEmail { get; set; }
    public string StudentPhone { get; set; }
    public string StudentGender { get; set; }
    public string VerifyStatus { get; set; }
    public string StudentPassword { get; set; }
    // Other properties
    public IFormFile? Photo { get; set; }
    public string? PhotoURL { get; set; }
}
public class RegisterVM : StudentVM
{
    [Required(ErrorMessage = "Confirm Password is required")]
    [Compare("StudentPassword", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; }
}

public class UpdatePasswordVM
{
    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string Current { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string New { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("New")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string Confirm { get; set; }
}

public class UpdateProfileVM
{
    public string? StudentEmail { get; set; }

    [StringLength(100)]
    public string StudentName { get; set; }

    public string? StudentAvatarURL { get; set; }

    [Required(ErrorMessage = "Phone number is required")]
    [Phone]
    [MaxLength(15)]
    [MinLength(11, ErrorMessage = "Phone number must be atleast 11 numbers long")]
    public string StudentPhone { get; set; }
    public string StudentGender { get; set; }
    public IFormFile? Photo { get; set; }
}

public class ResetPasswordVM
{
    [StringLength(100)]
    [EmailAddress]
    public string StudentEmail { get; set; }
}

public class AttendanceDetailsViewModel
{
    public string ClassName { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Status { get; set; }
}

public class ClassRegistrationViewModel
{
    public string ClassId { get; set; }
    public string ClassName { get; set; }
    public string ClassDescription { get; set; }
    public string Status { get; set; }
    public bool IsSelected { get; set; } // To track selected class
}
// ------------------------------------------------------------------------
// Login VM
// ------------------------------------------------------------------------
public class LoginVM
{
    [Required(ErrorMessage = "ID is required")]
    public string UserId { get; set; }

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; }
}
// ------------------------------------------------------------------------
// Tutor VM
// ------------------------------------------------------------------------
public class TutorVM
{
    public string TutorId { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [MaxLength(100)]
    public string TutorName { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress]
    [MaxLength(50)]
    public string TutorEmail { get; set; }

    [Required(ErrorMessage = "Password is required")]
    [MaxLength(50)]
    public string TutorPassword { get; set; }

    [Required(ErrorMessage = "Phone number is required")]
    [MaxLength(15)]
    public string TutorPhone { get; set; }

    [Required(ErrorMessage = "Please select gender")]
    [MaxLength(1)]
    public string TutorGender { get; set; }

    [Required(ErrorMessage = "Please select a photo")]
    public IFormFile TutorAvatarURL { get; set; }

    public List<string> SelectedSubjects { get; set; } = new();
}

public class TutorUpdateVM
{
    public string TutorId { get; set; }
    public string TutorName { get; set; }
    public string TutorEmail { get; set; }
    public string TutorPhone { get; set; }
    public string TutorGender { get; set; }
    public string TutorPassword { get; set; }
    // Other properties
    public IFormFile? Photo { get; set; }
    public string? PhotoURL { get; set; }
}

// ------------------------------------------------------------------------
// Subject VM
// ------------------------------------------------------------------------
public class SubjectVM
{
    public string SubjectId { get; set; }

    [Required(ErrorMessage = "Subject name is required")]
    [MaxLength(100)]
    public string SubjectName { get; set; }

    [MaxLength(100)]
    public string SubjectDescription { get; set; }
}
// ------------------------------------------------------------------------
// Class VM
// ------------------------------------------------------------------------
public class ClassVM
{
    public string ClassId { get; set; }

    [Required(ErrorMessage = "Class name is required")]
    [MaxLength(50)]
    public string ClassName { get; set; }

    [MaxLength(255)]
    public string ClassDescription { get; set; }

    [Required(ErrorMessage = "Status is required")]
    [MaxLength(50)]
    public string Status { get; set; }

    public List<string> SelectedStudents { get; set; } = new();
}
// ------------------------------------------------------------------------
// Schedule VM
// ------------------------------------------------------------------------
public class ScheduleVM
{
    public string ScheduleId { get; set; }

    [Required(ErrorMessage = "Date is required")]
    public DateTime Date { get; set; }

    [Required(ErrorMessage = "Time is required")]
    public TimeSpan Time { get; set; }

    [Required(ErrorMessage = "Start Time is required")]
    public TimeSpan StartTime { get; set; }

    [Required(ErrorMessage = "End Time is required")]
    public TimeSpan EndTime { get; set; }

    [Required(ErrorMessage = "Class is required")]
    public string ClassId { get; set; }

    // Changed from private set to public set
    public string AttendanceCode { get; set; }

    public List<string> SelectedSubjectIds { get; set; } = new();

    public Dictionary<string, string> SubjectTutorAssignments { get; set; } = new();
}
// ------------------------------------------------------------------------
// Schedule Subject Tutor VM
// ------------------------------------------------------------------------
public class ScheduleSubjectTutorVM
{
    public string ScheduleId { get; set; }

    [Required(ErrorMessage = "Subject is required")]
    public string SubjectId { get; set; }

    [Required(ErrorMessage = "Tutor is required")]
    public string TutorId { get; set; }

    // Optional: Add additional properties for display or validation
    public string SubjectName { get; set; }
    public string TutorName { get; set; }
}
// ------------------------------------------------------------------------
// Dashboard VM
// ------------------------------------------------------------------------
public class DashboardViewModel
{
    public int StudentCount { get; set; }
    public int TeacherCount { get; set; }
    public int ClassCount { get; set; }
    public int SubjectCount { get; set; }
    public int ActiveClassCount { get; set; }
    public int InactiveClassCount { get; set; }
    public int PendingStudentCount { get; set; }
    public int VerifiedStudentCount { get; set; }

    // Initialize dictionaries to avoid null reference exceptions
    public Dictionary<string, int> StudentsPerClass { get; set; } = new();
    public Dictionary<string, int> TutorsPerSubject { get; set; } = new();
    public Dictionary<string, int> StudentGenderDistribution { get; set; } = new();
    public Dictionary<string, int> ClassStatusDistribution { get; set; } = new();
    public Dictionary<string, int> AttendanceTrends { get; set; } = new();
    public Dictionary<string, int> WeeklyScheduleDistribution { get; set; } = new();
}


// ------------------------------------------------------------------------
// AttendanceReport VM
// ------------------------------------------------------------------------
public class AttendanceReportVM
{
    public string StudentId { get; set; }
    public string StudentName { get; set; }
    public string ClassId { get; set; }
    public string ClassName { get; set; }
    public int TotalDays { get; set; }
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public double AttendanceRate { get; set; }
}

// ------------------------------------------------------------------------
// AdminProfile VM
// ------------------------------------------------------------------------
public class AdminProfileVM
{
    public string AdminId { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [MaxLength(100)]
    public string AdminName { get; set; }

    public IFormFile? Photo { get; set; }
    public string? PhotoURL { get; set; }

    // Password properties
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
    [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
    public string? ConfirmPassword { get; set; }
}

// ------------------------------------------------------------------------
// WHW
// ------------------------------------------------------------------------

public class TodayClassViewModel
{
    public string ClassName { get; set; }
    public TimeSpan Time { get; set; }
    public string FormattedTime => Time.ToString(@"hh\:mm");  // Add a formatted time property
    public string AttendanceCode { get; set; }
    public List<Subject> Subjects { get; set; } = new();
    public int StudentCount { get; set; }
}

public class ClassDetailViewModel
{
    public string ClassName { get; set; }
    public string Subject { get; set; }
    public string Time { get; set; }
    public int StudentCount { get; set; }
    public string ScheduleId { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int NotMarkedCount { get; set; }
    public double AttendanceRate { get; set; }
    public List<StudentAttendanceViewModel> Students { get; set; } = new();
}

public class AttendanceUpdateModel
{
    public string ScheduleId { get; set; }
    public List<AttendanceData> AttendanceData { get; set; }
}

public class AttendanceData
{
    public string StudentId { get; set; }
    public string Status { get; set; }
}

public class ClassAttendanceViewModel
{
    public string ClassName { get; set; }
    public string ClassId { get; set; }
    public string ScheduleId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Status { get; set; }
}

public class StudentAttendanceViewModel
{
    public string StudentId { get; set; }
    public string StudentName { get; set; }
    public string StudentEmail { get; set; }   
    public string StudentPhone { get; set; }   
    public string Status { get; set; }
    public string AttendanceId { get; set; }
}

public class StudentViewModel
{
    public string StudentId { get; set; }
    public string StudentName { get; set; }
    public string StudentEmail { get; set; }
    public string StudentPhone { get; set; }
    public string VerifyStatus { get; set; }
    public List<string> Classes { get; set; }
}

public class ClassViewModel
{
    public string ClassId { get; set; }
    public string ClassName { get; set; }
    public int StudentCount { get; set; }
}