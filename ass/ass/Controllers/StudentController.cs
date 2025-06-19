using System.Net.Mail;
using System.Security.Claims;
using ass.Models;
using Demo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace ass.Controllers
{
    public class StudentController : Controller
    {
        private readonly DB db;
        private readonly IWebHostEnvironment en;
        private readonly Helper hp;
        private readonly IDistributedCache _redis;

        public StudentController(DB db, IWebHostEnvironment en, Helper hp, IDistributedCache r)
        {
            this.db = db;
            this.en = en;
            this.hp = hp;
            this._redis = r;
        }

        [Authorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        // GET: Student/Index
        public IActionResult Index()
        {
            var model = db.Students;
            return View(model);
        }

        private string NextId()
        {
            // Get the current max ID
            var maxId = db.Students
                .OrderByDescending(s => s.StudentId)
                .FirstOrDefault()?.StudentId ?? "S000";

            int nextNumber;
            if (int.TryParse(maxId[1..], out int lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
            else
            {
                nextNumber = 1;
            }

            // Check if the generated ID exists, if so, keep incrementing until we find a free one
            string newId;
            do
            {
                newId = $"S{nextNumber:D3}";
                nextNumber++;
            } while (db.Students.Any(s => s.StudentId == newId));

            return newId;
        }

        public IActionResult Register()
        {
            var vm = new RegisterVM
            {
                StudentId = NextId(),
            };

            return View(vm);
        }

        [HttpPost]
        public IActionResult Register(RegisterVM vm)
        {
            if (!ModelState.IsValid)
            {
                vm.StudentId = NextId();
                return View(vm);
            }

            if (vm.StudentAvatarURL == null)
            {
                ModelState.AddModelError("StudentAvatarURL", "Please select a photo.");
                return View(vm);
            }

            var error = hp.ValidatePhoto(vm.StudentAvatarURL);
            if (!string.IsNullOrEmpty(error))
            {
                ModelState.AddModelError("StudentAvatarURL", error);
                return View(vm);
            }

            try
            {
                var student = new Student
                {
                    StudentId = NextId(),
                    StudentName = vm.StudentName?.Trim().ToUpper() ?? "",
                    StudentEmail = vm.StudentEmail?.Trim() ?? "",
                    StudentPhone = vm.StudentPhone?.Trim() ?? "",
                    StudentPassword = hp.HashPassword(vm.StudentPassword), // Hash the password here
                    VerifyStatus = "Pending",
                    StudentGender = vm.StudentGender,
                    StudentAvatarURL = hp.SavePhoto(vm.StudentAvatarURL, "Students")
                };

                db.Students.Add(student);
                db.SaveChanges();

                // Generate a token and store it in Redis
                var token = Guid.NewGuid().ToString();
                _redis.SetString($"VerifyToken:{student.StudentId}", token, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) // 1-hour expiry
                });

                // Send the verification email
                SendVerificationEmail(student, token);

                TempData["Info"] = $"Register successfully! Your Student Id is {student.StudentId}. Please verify your email.";
                return RedirectToAction("Register");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Unable to register: " + ex.Message);
                return View(vm);
            }
        }



        [Authorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult UpdatePassword()
        {
            return View();
        }

        [Authorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [HttpPost]
        public IActionResult UpdatePassword(UpdatePasswordVM vm)
        {
            // Retrieve StudentId from claims
            var studentId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(studentId))
                return RedirectToAction("Index", "Student");

            // Find the student using the primary key (StudentId)
            var u = db.Students.Find(studentId);

            if (u == null)
                return RedirectToAction("Index", "Student");

            // If current password doesn't match
            if (!hp.VerifyPassword(u.StudentPassword, vm.Current))
            {
                ModelState.AddModelError("Current", "Current Password not matched.");
            }

            if (ModelState.IsValid)
            {
                // Update user password (hash)
                u.StudentPassword = hp.HashPassword(vm.New);
                db.SaveChanges();

                TempData["Info"] = "Password updated.";
                return RedirectToAction();
            }

            return View();
        }

        [Authorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult UpdateProfile()
        {
            // Retrieve StudentId from claims
            var studentId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(studentId))
                return RedirectToAction("Index", "Student");

            // Find the student using the primary key
            var m = db.Students.Find(studentId);

            if (m == null)
                return RedirectToAction("Index", "Student");

            var vm = new UpdateProfileVM
            {
                StudentEmail = m.StudentEmail,
                StudentName = m.StudentName,
                StudentPhone = m.StudentPhone,
                StudentGender = m.StudentGender,
                StudentAvatarURL = m.StudentAvatarURL,
            };

            return View(vm);
        }

        [Authorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [HttpPost]
        public IActionResult UpdateProfile(UpdateProfileVM vm)
        {
            // Retrieve StudentId from claims
            var studentId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(studentId))
                return RedirectToAction("Index", "Student");

            // Find the student using the primary key
            var m = db.Students.Find(studentId);

            if (m == null)
                return RedirectToAction("Index", "Student");

            if (vm.Photo != null)
            {
                var err = hp.ValidatePhoto(vm.Photo);
                if (!string.IsNullOrEmpty(err))
                {
                    ModelState.AddModelError("Photo", err);
                }
            }

            if (ModelState.IsValid)
            {
                m.StudentName = vm.StudentName;
                m.StudentGender = vm.StudentGender;
                m.StudentPhone = vm.StudentPhone;

                if (vm.Photo != null)
                {
                    hp.DeletePhoto(m.StudentAvatarURL, "Students");
                    m.StudentAvatarURL = hp.SavePhoto(vm.Photo, "Students");
                }

                db.SaveChanges();

                TempData["Info"] = "Profile updated.";
                return RedirectToAction();
            }

            vm.StudentEmail = m.StudentEmail;
            vm.StudentAvatarURL = m.StudentAvatarURL;
            return View(vm);
        }

        public IActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ResetPassword(ResetPasswordVM vm)
        {
            var u = db.Students.FirstOrDefault(s => s.StudentEmail == vm.StudentEmail);

            if (u == null)
            {
                ModelState.AddModelError("StudentEmail", "Email not found.");
            }

            if (ModelState.IsValid)
            {
                string password = hp.RandomPassword();

                u!.StudentPassword = hp.HashPassword(password);
                db.SaveChanges();

                // Send reset password email
                SendResetPasswordEmail(u, password);

                TempData["Info"] = $"Password reset. Check your email.";
                return RedirectToAction();
            }

            return View();
        }

        private void SendResetPasswordEmail(Student u, string password)
        {
            var mail = new MailMessage();
            mail.To.Add(new MailAddress(u.StudentEmail, u.StudentName));
            mail.Subject = "Reset Password";
            mail.IsBodyHtml = true;

            var url = Url.Action("Login", "Auth", null, "https");

            var path = u switch
            {
                Student m => Path.Combine(en.WebRootPath, "Students", m.StudentAvatarURL),
                _ => "",
            };

            var att = new Attachment(path);
            mail.Attachments.Add(att);
            att.ContentId = "photo";

            mail.Body = $@"
                <img src='cid:photo' style='width: 200px; height: 200px;
                                            border: 1px solid #333'>
                <p>Dear {u.StudentName},<p>
                <p>Your password has been reset to:</p>
                <h1 style='color: red'>{password}</h1>
                <p>
                    Please <a href='{url}'>login</a>
                    with your new password.
                </p>
                <p>From, 🎓 TARUMT Tuition</p>
            ";

            hp.SendEmail(mail);
        }

        private void SendVerificationEmail(Student student, string token)
        {
            var mail = new MailMessage();
            mail.To.Add(new MailAddress(student.StudentEmail, student.StudentName));
            mail.Subject = "Email Verification";
            mail.IsBodyHtml = true;

            // Generate the verification URL
            var url = Url.Action("VerifyEmail", "Student", new { studentId = student.StudentId, token }, "https");

            // Prepare the path for the student avatar
            var path = student.StudentAvatarURL != null
                ? Path.Combine(en.WebRootPath, "Students", student.StudentAvatarURL)
                : "";

            var att = new Attachment(path);
            mail.Attachments.Add(att);
            att.ContentId = "photo";

            // Email body with the verification button
            mail.Body = $@"
                <img src='cid:photo' style='width: 200px; height: 200px; border: 1px solid #333'>
                <p>Dear {student.StudentName},</p>
                <p>Thank you for registering. Please verify your email address to activate your account.</p>
                <a href='{url}' 
                   style='display: inline-block; padding: 10px 20px; color: white; background-color: #28a745; 
                          text-decoration: none; border-radius: 5px; font-size: 16px;'>
                   Verify Email
                </a>
                <p>If you did not sign up for this account, please ignore this email.</p>
                <p>From, 🎓 TARUMT Tuition</p>
            ";

            // Send the email
            hp.SendEmail(mail);
        }

        [HttpGet]
        public IActionResult VerifyEmail(string studentId, string token)
        {
            // Retrieve the token from Redis
            var storedToken = _redis.GetString($"VerifyToken:{studentId}");
            if (storedToken == null || storedToken != token)
            {
                return Content("Invalid or expired token.");
            }

            // Update the student's VerifyStatus
            var student = db.Students.SingleOrDefault(s => s.StudentId == studentId);
            if (student == null)
            {
                return Content("Student not found.");
            }

            student.VerifyStatus = "Verified";
            db.SaveChanges();

            // Remove the token from Redis
            _redis.Remove($"VerifyToken:{studentId}");

            return Content("Your email has been verified successfully!");
        }

        [Authorize]
        public IActionResult ClassTimetable(int month, int year)
        {
            // Retrieve the logged-in student's ID from claims
            var studentId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized(); // If the student ID is not found, deny access
            }

            // Fetch the student and their registered classes
            var student = db.Students.Include(s => s.Classes)
                                      .FirstOrDefault(s => s.StudentId == studentId);
            if (student == null)
            {
                return Unauthorized(); // If student is not found, deny access
            }

            // Fetch registered class IDs
            var registeredClassIds = student.Classes.Select(c => c.ClassId).ToList();

            // Default min and max to current year
            int min = DateTime.Today.Year;
            int max = DateTime.Today.Year;

            if (db.Schedules.Any())
            {
                min = db.Schedules.Min(e => e.Date.Year);
                max = db.Schedules.Max(e => e.Date.Year);
            }

            // If month or year is out of range
            if (month < 1 || month > 12 || year < min || year > max)
            {
                month = DateTime.Today.Month;
                year = max;
                return RedirectToAction(null, new { month, year });
            }

            // Pass month and year to UI
            ViewBag.Month = month;
            ViewBag.Year = year;

            // For selection lists
            ViewBag.MonthList = hp.GetMonthList();
            ViewBag.YearList = hp.GetYearList(min, max);

            // Create the dictionary for the timetable
            var dict = new Dictionary<DateOnly, List<Schedule>>();

            // First day (a) and last day (b) of the month
            var a = new DateOnly(year, month, 1);
            var b = a.AddMonths(+1).AddDays(-1);

            // Adjustment --> first day = Monday, last day = Sunday
            while (a.DayOfWeek != DayOfWeek.Monday) a = a.AddDays(-1);
            while (b.DayOfWeek != DayOfWeek.Sunday) b = b.AddDays(+1);

            // Fill dictionary with keys (dates) and values (events)
            for (var d = a; d <= b; d = d.AddDays(+1))
            {
                dict[d] = db.Schedules
                            .Include(s => s.Class) // Include Class entity
                            .Where(e => e.Date.Date == d.ToDateTime(TimeOnly.MinValue).Date
                                        && registeredClassIds.Contains(e.ClassId) // Only student's classes
                                        && e.Class.Status == "Active") // Only active classes
                            .ToList();
            }

            return View(dict);
        }



        [Authorize]
        public IActionResult SubjectReg()
        {
            return View();
        }

        [Authorize]
        public IActionResult ClassDetails(DateOnly date)
        {
            // Retrieve the logged-in student's ID from claims
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized(); // If the student ID is not found, deny access
            }

            // Fetch the student's registered classes
            var student = db.Students.Include(s => s.Classes)
                                      .FirstOrDefault(s => s.StudentId == studentId);
            if (student == null)
            {
                return Unauthorized(); // If student is not found, deny access
            }

            // Fetch registered class IDs
            var registeredClassIds = student.Classes.Select(c => c.ClassId).ToList();

            // Fetch schedules for the given date and filter by registered classes
            var targetDate = date.ToDateTime(TimeOnly.MinValue);
            var schedules = db.Schedules
                              .Include(s => s.Class)
                              .Include(s => s.Attendances)
                              .Where(s => s.Date.Year == targetDate.Year 
                                     && s.Date.Month == targetDate.Month 
                                     && s.Date.Day == targetDate.Day
                                     && registeredClassIds.Contains(s.ClassId)
                                     && s.Class.Status == "Active")
                              .ToList();

            // Pass the student ID and schedules to the view
            ViewBag.StudentId = studentId;
            return View(schedules);
        }


        // Display the Attendance page
        [Authorize]
        public IActionResult TakeAttendance(string scheduleId)
        {
            if (string.IsNullOrEmpty(scheduleId) || !db.Schedules.Any(s => s.ScheduleId == scheduleId))
            {
                return NotFound(); // Return 404 if schedule doesn't exist
            }

            // Pass scheduleId to the view (do not treat scheduleId as view name)
            return View("TakeAttendance", scheduleId); // Make sure the view name is explicitly defined
        }



        // Handle Attendance Submission
        [HttpPost]
        [Authorize]
        public IActionResult SubmitAttendanceCode(string attendanceCode, string scheduleId)
        {
            var schedule = db.Schedules.FirstOrDefault(s => s.AttendanceCode == attendanceCode);
            if (schedule == null)
            {
                TempData["Info"] = "Invalid attendance code.";
                return RedirectToAction("TakeAttendance", new { scheduleId = scheduleId });
            }

            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Check if attendance already exists for this student and schedule
            var existingAttendance = db.Attendances.FirstOrDefault(a =>
                a.StudentId == studentId &&
                a.ScheduleId == schedule.ScheduleId &&
                a.Date.Date == DateTime.Today);

            if (existingAttendance != null)
            {
                TempData["Info"] = "Attendance already recorded for this class today.";
                return RedirectToAction("Index");
            }

            try
            {
                // Generate new ID inside a transaction to prevent race conditions
                using var transaction = db.Database.BeginTransaction();

                var lastAttendance = db.Attendances
                    .OrderByDescending(a => a.AttendanceId)
                    .FirstOrDefault();

                int newIdNumber = 1;
                if (lastAttendance != null && lastAttendance.AttendanceId.StartsWith("ATT"))
                {
                    if (int.TryParse(lastAttendance.AttendanceId[3..], out int lastIdNumber))
                    {
                        newIdNumber = lastIdNumber + 1;
                    }
                }

                string newAttendanceId = $"ATT{newIdNumber:D3}";

                // Verify the generated ID is unique
                while (db.Attendances.Any(a => a.AttendanceId == newAttendanceId))
                {
                    newIdNumber++;
                    newAttendanceId = $"ATT{newIdNumber:D3}";
                }

                var attendance = new Attendance
                {
                    AttendanceId = newAttendanceId,
                    StudentId = studentId,
                    ScheduleId = schedule.ScheduleId,
                    Date = DateTime.Now,
                    IsPresent = "Present",
                    Status = "Present"
                };

                db.Attendances.Add(attendance);
                db.SaveChanges();
                transaction.Commit();

                TempData["Info"] = "Attendance recorded successfully.";
            }
            catch (Exception ex)
            {
                // Log the error if you have logging configured
                TempData["Info"] = "Unable to record attendance. Please try again.";
            }

            return RedirectToAction("Index");
        }

        [Authorize]
        public IActionResult AttendanceDetails()
        {
            // Get the current student ID from claims
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized(); // Return 401 if student ID is not found
            }

            // Get attendance records for the logged-in student until today
            var attendanceDetails = db.Attendances
                .Include(a => a.Schedule)
                .ThenInclude(s => s.Class)
                .Where(a => a.StudentId == studentId && a.Date.Date <= DateTime.Now.Date)
                .Select(a => new AttendanceDetailsViewModel
                {
                    ClassName = a.Schedule.Class.ClassName,
                    Date = a.Date,
                    StartTime = a.Schedule.StartTime,
                    EndTime = a.Schedule.EndTime,
                    Status = a.IsPresent == "Present" ? "Present" : "Absent"
                })
                .OrderBy(a => a.Date)
                .ToList();

            // Calculate total schedules until today
            var totalSchedules = db.Schedules
                .Where(s => s.Class.Students.Any(st => st.StudentId == studentId) && s.Date.Date <= DateTime.Now.Date)
                .Count();

            // Calculate attended schedules until today
            var attendedSchedules = db.Attendances
                .Where(a => a.StudentId == studentId && a.IsPresent == "Present" && a.Date.Date <= DateTime.Now.Date)
                .Count();

            // Pass counts to the view using ViewBag
            ViewBag.TotalSchedules = totalSchedules;
            ViewBag.AttendedSchedules = attendedSchedules;

            return View(attendanceDetails);
        }



        [Authorize]
        public IActionResult ClassReg()
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            // Fetch the student with their registered classes
            var student = db.Students.Include(s => s.Classes)
                .FirstOrDefault(s => s.StudentId == studentId);

            if (student == null)
            {
                return Unauthorized();
            }

            // Load available classes
            var availableClasses = db.Classes
                .AsEnumerable() // Switch to in-memory evaluation
                .Select(c => new ClassRegistrationViewModel
                {
                    ClassId = c.ClassId,
                    ClassName = c.ClassName,
                    ClassDescription = c.ClassDescription,
                    Status = c.Status,
                    // Check if the student is already registered for the class
                    IsSelected = student.Classes.Any(sc => sc.ClassId == c.ClassId)
                })
                .ToList();

            return View(availableClasses);
        }




        [HttpPost]
        [Authorize]
        public IActionResult ClassReg(string classId, string dropClassId)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var student = db.Students.Include(s => s.Classes)
                .FirstOrDefault(s => s.StudentId == studentId);

            if (student == null)
            {
                return Unauthorized();
            }

            if (!string.IsNullOrEmpty(classId))
            {
                // Register the student for a class
                var selectedClass = db.Classes.FirstOrDefault(c => c.ClassId == classId && c.Status == "Active");
                if (selectedClass != null && !student.Classes.Contains(selectedClass))
                {
                    student.Classes.Add(selectedClass);
                    db.SaveChanges();
                }
            }
            else if (!string.IsNullOrEmpty(dropClassId))
            {
                // Drop the class the student is registered for
                var dropClass = student.Classes.FirstOrDefault(c => c.ClassId == dropClassId);
                if (dropClass != null)
                {
                    student.Classes.Remove(dropClass);
                    db.SaveChanges();
                }
            }

            return RedirectToAction("ClassReg"); // Refresh the class list after action
        }






    }
}