using Demo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;

namespace ass.Controllers;

[Authorize(Roles = "Tutor")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class TutorController : Controller
{
    private string GetTutorId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;
    private readonly IDistributedCache _redis;
    public TutorController(DB db, IWebHostEnvironment en, Helper hp, IDistributedCache r)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
        this._redis = r;
    }
    //=============================================================================================================================================

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var tutorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (tutorId != null)
        {
            var tutor = db.Tutors.FirstOrDefault(t => t.TutorId == tutorId);
            ViewBag.TutorName = tutor?.TutorName;
            ViewBag.TutorAvatarURL = tutor?.TutorAvatarURL;
        }
        base.OnActionExecuting(context);
    }
    //================================================= viewStudent ============================================================================================
    // GET: Tutor/viewStudent
    [Authorize]
    //
    public IActionResult viewStudent()
    {
        var tutorId = GetTutorId();
        if (tutorId == null) return RedirectToAction("Login", "Auth");

        var tutorClasses = db.Schedules
            .Include(s => s.Class)
            .Include(s => s.Subjects)
            .Where(s => s.Subjects.Any(sub => sub.Tutors.Any(t => t.TutorId == tutorId)))
            .Select(s => new ClassViewModel
            {
                ClassName = s.Class.ClassName,
                ClassId = s.Class.ClassId,
                StudentCount = s.Class.Students.Count
            })
            .Distinct()
            .ToList();

        return View(tutorClasses);
    }

    // GET: Tutor/viewStudentsByClass
    [Authorize]
    public IActionResult viewStudentsByClass(string classId)
    {
        var tutorId = GetTutorId();
        if (tutorId == null) return RedirectToAction("Login", "Auth");

        var students = db.Students
            .Where(s => s.Classes.Any(c => c.ClassId == classId))
            .Select(s => new StudentViewModel
            {
                StudentId = s.StudentId,
                StudentName = s.StudentName,
                StudentEmail = s.StudentEmail,
                StudentPhone = s.StudentPhone,
                VerifyStatus = s.VerifyStatus
            })
            .ToList();

        ViewBag.ClassName = db.Classes.Find(classId)?.ClassName;
        return View(students);
    }

    //================================================= Dashboard ============================================================================================
    // GET: Tutor/Dashboard
    [Authorize]
    public IActionResult Dashboard()
    {
        var tutorId = GetTutorId();
        if (tutorId == null) return RedirectToAction("Login", "Auth");

        // Get today's date
        var today = DateTime.Today;

        try
        {
            // Get today's classes that this tutor is teaching
            var todayClasses = db.Schedules
                .Include(s => s.Class)
                    .ThenInclude(c => c.Students)
                .Include(s => s.Subjects)
                .Where(s => s.Date.Date == today.Date &&
                           s.Subjects.Any(sub => sub.Tutors.Any(t => t.TutorId == tutorId)))
                .Select(s => new TodayClassViewModel
                {
                    ClassName = s.Class.ClassName,
                    Time = s.Time,
                    AttendanceCode = s.AttendanceCode ?? "Not Generated",
                    Subjects = s.Subjects.ToList(),
                    StudentCount = s.Class.Students.Count
                })
                .ToList();

            // Get total students count
            var studentCount = db.Students.Count();

            // Get class count by querying through schedules
            var classCount = db.Schedules
                .Where(s => s.Class.Status == "Active" &&
                           s.Subjects.Any(sub => sub.Tutors.Any(t => t.TutorId == tutorId)))
                .Select(s => s.Class.ClassId)
                .Distinct()
                .Count();

            ViewBag.TutorId = tutorId;
            ViewBag.TodayClasses = todayClasses;
            ViewBag.StudentCount = studentCount;
            ViewBag.ClassCount = classCount;

            return View();
        }
        catch (Exception ex)
        {
            // Log the error (you should implement proper logging)
            Console.WriteLine($"Error in Dashboard: {ex.Message}");

            // Return empty lists if there's an error
            ViewBag.TutorId = tutorId;
            ViewBag.TodayClasses = new List<TodayClassViewModel>();
            ViewBag.StudentCount = 0;
            ViewBag.ClassCount = 0;

            return View();
        }
    }

    //================================================= Profile ============================================================================================
    [Authorize]
    // GET: Tutor/Profile

    public IActionResult Profile()
    {
        var tutorId = GetTutorId();

        if (tutorId == null) return RedirectToAction("Login", "Auth");

        var tutor = db.Tutors.FirstOrDefault(t => t.TutorId == tutorId);

        ViewBag.TutorName = tutor?.TutorName;
        ViewBag.TutorAvatarURL = tutor?.TutorAvatarURL;

        return View(tutor);
    }


    // POST: Tutor/Profile
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Profile(string name, string email, string phone, IFormFile photo)
    {
        var tutorId = GetTutorId();

        if (tutorId == null) return RedirectToAction("Login", "Auth");

        var tutor = db.Tutors
            .FirstOrDefault(t => t.TutorId == tutorId);

        if (tutor == null)
        {
            TempData["Error"] = "Unauthorized access";
            return RedirectToAction("Login", "Auth");
        }

        // Create a TutorVM for validation
        var tutorVM = new TutorVM
        {
            TutorId = tutorId,
            TutorName = name?.Trim(),
            TutorEmail = email?.Trim(),
            TutorPhone = phone?.Trim(),
            TutorGender = tutor.TutorGender // Keep existing gender
        };

        // Handle photo upload if provided
        if (photo != null)
        {
            var photoError = hp.ValidatePhoto(photo);
            if (!string.IsNullOrEmpty(photoError))
            {
                TempData["Error"] = photoError;
                return View(tutor);
            }

            // Delete old photo if exists
            if (!string.IsNullOrEmpty(tutor.TutorAvatarURL))
            {
                hp.DeletePhoto(tutor.TutorAvatarURL, "Tutors");
            }

            // Save new photo
            tutor.TutorAvatarURL = hp.SavePhoto(photo, "Tutors");
        }

        // Update tutor information
        tutor.TutorName = tutorVM.TutorName;
        tutor.TutorEmail = tutorVM.TutorEmail;
        tutor.TutorPhone = tutorVM.TutorPhone;

        try
        {
            db.SaveChanges();
            TempData["Success"] = "Profile updated successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "An error occurred while updating the profile.";
            // Log the error
            Console.WriteLine($"Error updating tutor profile: {ex.Message}");
        }

        return RedirectToAction("Profile");
    }
    //================================================= Timetable ============================================================================================
    // POST: Tutor/tutorTimetable
    [Authorize]

    [HttpGet]
    public IActionResult tutorTimetable(int month, int year)
    {
        var tutorId = GetTutorId();

        if (tutorId == null) return RedirectToAction("Login", "Auth");

        // Default to current year
        int min = DateTime.Today.Year;
        int max = DateTime.Today.Year;

        if (db.Schedules.Any())
        {
            min = db.Schedules.Min(e => e.Date.Year);
            max = db.Schedules.Max(e => e.Date.Year);
        }

        // If month or year is out of range
        // --> Redirect with current month and max year
        if (month < 1 || month > 12 || year < min || year > max)
        {
            month = DateTime.Today.Month;
            year = max;
            return RedirectToAction(null, new { month, year });
        }

        // Pass month and year to UI
        ViewBag.Month = month;
        ViewBag.Year = year;
        ViewBag.MonthList = hp.GetMonthList();
        ViewBag.YearList = hp.GetYearList(min, max);

        var dict = new Dictionary<DateOnly, List<Schedule>>();

        // First day (a) and last day (b) of the month
        var a = new DateOnly(year, month, 1);
        var b = a.AddMonths(+1).AddDays(-1);

        // Adjustment --> first day = Monday, last day = Sunday
        while (a.DayOfWeek != DayOfWeek.Monday) a = a.AddDays(-1);
        while (b.DayOfWeek != DayOfWeek.Sunday) b = b.AddDays(+1);

        // Fill dictionary with keys (dates) and values (events)
        // Modified to only show classes where this tutor teaches
        for (var d = a; d <= b; d = d.AddDays(+1))
        {
            dict[d] = db.Schedules
                        .Include(s => s.Class)
                        .Include(s => s.Subjects)
                            .ThenInclude(sub => sub.Tutors)
                        .Where(e => e.Date.Date == d.ToDateTime(TimeOnly.MinValue).Date &&
                                   e.Subjects.Any(sub => sub.Tutors.Any(t => t.TutorId == tutorId)))
                        .ToList();
        }

        return View(dict);
    }
    //================================================= Manage Attendance ============================================================================================
    [Authorize]
    // GET: Tutor/ViewClass
    public IActionResult ViewClass(DateTime? date = null)
    {
        var tutorId = GetTutorId();

        if (tutorId == null) return RedirectToAction("Login", "Auth");
        date ??= DateTime.Today;

        var classes = db.Schedules
            .Include(s => s.Class)
            .Include(s => s.Subjects)
            .Where(s => s.Date.Date == date.Value.Date &&
                       s.Subjects.Any(sub => sub.Tutors.Any(t => t.TutorId == tutorId)))
            .Select(s => new ClassAttendanceViewModel
            {
                ClassName = s.Class.ClassName,
                ClassId = s.ClassId,
                ScheduleId = s.ScheduleId,
                Date = s.Date,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Status = s.Class.Status
            })
            .OrderBy(c => c.StartTime)
            .ToList();

        ViewBag.SelectedDate = date.Value.ToString("yyyy-MM-dd");
        return View(classes);
    }

    // POST: Tutor/ViewClass
    [Authorize]
    [HttpPost]
    public IActionResult ViewClass(DateTime dateTaken)
    {
        return RedirectToAction("ViewClass", new { date = dateTaken });
    }

    // Helper method to get attendance status
    private string GetAttendanceStatus(string studentId, string scheduleId)
    {
        if (string.IsNullOrEmpty(scheduleId)) return "N/A";

        var attendance = db.Attendances
            .FirstOrDefault(a => a.StudentId == studentId && a.ScheduleId == scheduleId);

        return attendance?.IsPresent ?? "N/A";
    }

    // GET: Tutor/ClassDetail
    [Authorize]
    public IActionResult ClassDetail(string className)
    {
        var tutorId = GetTutorId();

        if (tutorId == null) return RedirectToAction("Login", "Auth");

        var classDetails = db.Classes
            .Include(c => c.Students)
            .FirstOrDefault(c => c.ClassName == className);

        if (classDetails == null)
        {
            return RedirectToAction("Dashboard");
        }

        // Get the latest schedule
        var latestSchedule = db.Schedules
            .Include(s => s.Subjects)
            .Where(s => s.ClassId == classDetails.ClassId)
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.Time)
            .FirstOrDefault();

        // Format time with AM/PM
        string formattedTime = "N/A";
        if (latestSchedule != null)
        {
            var time = latestSchedule.Time;
            string period = time.Hours >= 12 ? "PM" : "AM";
            int hour = time.Hours > 12 ? time.Hours - 12 : (time.Hours == 0 ? 12 : time.Hours);
            formattedTime = $"{hour:D2}:{time.Minutes:D2} {period}";
        }

        // Get students with attendance status
        var students = classDetails.Students
            .Select(s => new StudentAttendanceViewModel
            {
                StudentId = s.StudentId,
                StudentName = s.StudentName,
                StudentEmail = s.StudentEmail,
                StudentPhone = s.StudentPhone,
                Status = GetAttendanceStatus(s.StudentId, latestSchedule?.ScheduleId)
            })
            .ToList();

        // Calculate attendance statistics
        int presentCount = students.Count(s => s.Status == "Present");
        int absentCount = students.Count(s => s.Status == "Absent");
        int notMarkedCount = students.Count(s => s.Status == "N/A");
        double attendanceRate = students.Any() ? (presentCount * 100.0 / students.Count) : 0;

        var viewModel = new ClassDetailViewModel
        {
            ClassName = classDetails.ClassName,
            Subject = latestSchedule?.Subjects.FirstOrDefault()?.SubjectName ?? "N/A",
            Time = formattedTime,
            StudentCount = students.Count,
            ScheduleId = latestSchedule?.ScheduleId,
            PresentCount = presentCount,
            AbsentCount = absentCount,
            NotMarkedCount = notMarkedCount,
            AttendanceRate = attendanceRate,
            Students = students
        };

        return View(viewModel);
    }

    // POST: Tutor/UpdateAttendance
    [Authorize]
    [HttpPost]
    public IActionResult UpdateAttendance([FromBody] AttendanceUpdateModel model)
    {
        try
        {
            var schedule = db.Schedules.Find(model.ScheduleId);
            if (schedule == null)
            {
                return Json(new { success = false, message = "Schedule not found." });
            }


            foreach (var attendance in model.AttendanceData)
            {
                var existingAttendance = db.Attendances
                    .FirstOrDefault(a => a.ScheduleId == model.ScheduleId &&
                                       a.StudentId == attendance.StudentId);

                if (existingAttendance == null)
                {
                    existingAttendance = new Attendance
                    {
                        AttendanceId = "ATT" + Guid.NewGuid().ToString().Substring(0, 7),
                        ScheduleId = model.ScheduleId,
                        StudentId = attendance.StudentId,
                        Date = DateTime.Now,
                        IsPresent = attendance.Status == "P" ? "Present" : "Absent",
                        Status = attendance.Status == "P" ? "Present" : "Absent",
                    };
                    db.Attendances.Add(existingAttendance);
                }
                else
                {
                    existingAttendance.IsPresent = attendance.Status == "P" ? "Present" : "Absent";
                    existingAttendance.Status = attendance.Status == "P" ? "Present" : "Absent";
                }
            }

            db.SaveChanges();
            return Json(new { success = true, message = "Attendance updated successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}