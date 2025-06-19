using Demo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using iTextSharp.text;
using iTextSharp.text.pdf;
using OfficeOpenXml;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using System.Net.Mail;

namespace ass.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class AdminController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;
    private readonly IDistributedCache _redis;

    public AdminController(DB db, IWebHostEnvironment en, Helper hp, IDistributedCache r)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
        this._redis = r;
    }

    //==================================================================================================
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (adminId != null)
        {
            var admin = db.Admins.FirstOrDefault(a => a.AdminId == adminId);
            ViewBag.AdminName = admin?.AdminName;
            ViewBag.AdminPhotoURL = admin?.AdminAvatarURL;
        }
        base.OnActionExecuting(context);
    }

    private string GetAdminId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private void SendUserUpdateEmail(string email, string name, string userType, string adminId, string photoUrl = null)
    {
        var mail = new MailMessage();
        mail.To.Add(new MailAddress(email, name));
        mail.Subject = $"{userType} Profile Update Notification";
        mail.IsBodyHtml = true;

        if (!string.IsNullOrEmpty(photoUrl))
        {
            var path = Path.Combine(en.WebRootPath, userType + "s", photoUrl);
            if (System.IO.File.Exists(path))
            {
                var att = new Attachment(path);
                mail.Attachments.Add(att);
                att.ContentId = "photo";
            }
            else
            {
                Console.WriteLine($"File not found: {path}");
            }
        }


        mail.Body = $@"
        <div style='font-family: Arial, sans-serif; padding: 20px;'>
            {(photoUrl != null ? $"<img src='cid:photo' style='width: 100px; height: 100px; border-radius: 50%;'><br/>" : "")}
            <h2>Profile Update Notification</h2>
            <p>Dear {name},</p>
            <p>Your profile has been updated by administrator (ID: {adminId}) on {DateTime.Now:dd/MM/yyyy HH:mm:ss}.</p>
            <p style='color: #e74c3c;'><strong>If you did not expect this change, please contact the administrator immediately.</strong></p>
            <hr>
            <p style='color: #7f8c8d;'>This is an automated message. Please do not reply.</p>
            <p>From,<br>🎓 TARUMT Tuition System</p>
        </div>";

        hp.SendEmail(mail);
    }
    //================================================= Profile ============================================================================================

    // GET: Admin/Profile
    public IActionResult Profile()
    {
        var adminId = GetAdminId();
        if (adminId == null) return RedirectToAction("Login", "Auth");

        var admin = db.Admins.FirstOrDefault(t => t.AdminId == adminId);
        if (admin == null) return RedirectToAction("Dashboard");

        ViewBag.AdminName = admin.AdminName;
        ViewBag.AdminAvatarURL = admin.AdminAvatarURL;

        var vm = new AdminProfileVM
        {
            AdminId = admin.AdminId,
            AdminName = admin.AdminName,
            PhotoURL = admin.AdminAvatarURL
        };

        return View(vm);
    }

    // POST: Admin/UpdateProfile
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateProfile(AdminProfileVM vm)
    {
        var adminId = GetAdminId();
        if (adminId == null) return RedirectToAction("Login", "Auth");

        var admin = db.Admins.FirstOrDefault(a => a.AdminId == adminId);
        if (admin == null) return RedirectToAction("Dashboard");

        if (!ModelState.IsValid) return View("Profile", vm);

        // Validate name if provided
        if (!string.IsNullOrEmpty(vm.AdminName) && !hp.IsValidName(vm.AdminName))
        {
            ModelState.AddModelError("AdminName", "Name can only contain letters and spaces");
            return View("Profile", vm);
        }

        admin.AdminName = vm.AdminName?.Trim();

        // Handle photo upload
        if (vm.Photo != null)
        {
            var photoError = hp.ValidatePhoto(vm.Photo);
            if (!string.IsNullOrEmpty(photoError))
            {
                ModelState.AddModelError("Photo", photoError);
                return View("Profile", vm);
            }

            // Delete old photo if exists
            if (!string.IsNullOrEmpty(admin.AdminAvatarURL))
            {
                hp.DeletePhoto(admin.AdminAvatarURL, "Admins");
            }

            // Save new photo
            admin.AdminAvatarURL = hp.SavePhoto(vm.Photo, "Admins");
        }

        db.SaveChanges();
        TempData["Success"] = "Profile updated successfully.";
        return RedirectToAction("Profile");
    }

    // POST: Admin/ChangePassword
    [HttpPost]
    public IActionResult ChangePassword(AdminProfileVM vm)
    {
        if (string.IsNullOrEmpty(vm.AdminId))
        {
            TempData["Error"] = "Invalid request.";
            return RedirectToAction("Profile");
        }

        var admin = db.Admins.Find(vm.AdminId);
        if (admin == null)
        {
            TempData["Error"] = "Admin not found.";
            return RedirectToAction("Dashboard");
        }

        // Validate current password
        if (!hp.VerifyPassword(admin.AdminPassword, vm.CurrentPassword))
        {
            TempData["Error"] = "Current password is incorrect.";
            return RedirectToAction("Profile");
        }

        try
        {
            if (hp.IsValidPassword(vm.NewPassword))
            {
                admin.AdminPassword = hp.HashPassword(vm.NewPassword);
                db.SaveChanges();
                TempData["Success"] = "Password updated successfully!";
                return RedirectToAction("Profile");
            }
            else {
                TempData["Error"] = "Password does not meet requirements";
                return RedirectToAction("Profile");
            }
           
        }
        catch (Exception)
        {
            TempData["Error"] = "Failed to update password. Please try again.";
            return RedirectToAction("Profile");
        }
    }

    //================================================= Dashboards ============================================================================================

    // GET: Admin/Dashboard
    [Authorize]
    public IActionResult Dashboard()
    {
        var adminId = GetAdminId();
        if (adminId == null) return RedirectToAction("Login", "Auth");

        // Get attendance data for the last 7 days
        var last7Days = DateTime.Today.AddDays(-7);
        var attendanceData = db.Attendances
            .Where(a => a.Date >= last7Days)
            .Select(a => new {
                Date = a.Date.Date,
                IsPresent = a.IsPresent
            })
            .ToList() // Execute query and bring data to memory
            .GroupBy(a => a.Date)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key.ToString("MM/dd"),
                g => new {
                    Total = g.Count(),
                    Present = g.Count(a => a.IsPresent == "Present")
                }
            )
            .ToDictionary(
                kvp => kvp.Key,
                kvp => (int)((double)kvp.Value.Present / kvp.Value.Total * 100)
            );

        // Get schedule distribution for next 7 days
        var next7Days = DateTime.Today.AddDays(7);
        var scheduleData = new Dictionary<string, int>();

        // Iterate through all days of the week
        foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
        {
            var date = DateTime.Today.AddDays((int)day - (int)DateTime.Today.DayOfWeek);
            var count = db.Schedules.Count(s => s.Date.Date == date.Date);
            scheduleData[day.ToString()] = count;
        }


        var viewModel = new DashboardViewModel
        {
            StudentCount = db.Students.Count(),
            TeacherCount = db.Tutors.Count(),
            ClassCount = db.Classes.Count(),
            SubjectCount = db.Subjects.Count(),
            ActiveClassCount = db.Classes.Count(c => c.Status == "Active"),
            InactiveClassCount = db.Classes.Count(c => c.Status == "Inactive"),
            PendingStudentCount = db.Students.Count(s => s.VerifyStatus == "Pending"),
            VerifiedStudentCount = db.Students.Count(s => s.VerifyStatus == "Verified"),

            // Students per Class
            StudentsPerClass = db.Classes
                .Include(c => c.Students)
                .ToDictionary(
                    c => c.ClassName,
                    c => c.Students.Count
                ),

            // Tutors per Subject
            TutorsPerSubject = db.Subjects
                .Include(s => s.Tutors)
                .ToDictionary(
                    s => s.SubjectName,
                    s => s.Tutors.Count
                ),

            // Student Gender Distribution
            StudentGenderDistribution = db.Students
                .GroupBy(s => s.StudentGender)
                .ToDictionary(
                    g => g.Key == "M" ? "Male" : "Female",
                    g => g.Count()
                ),

            // Class Status Distribution
            ClassStatusDistribution = db.Classes
                .GroupBy(c => c.Status)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count()
                ),

            AttendanceTrends = attendanceData,
            WeeklyScheduleDistribution = scheduleData
        };
        return View(viewModel);
    }
    //================================================= Students ============================================================================================
    // GET: Students/Check Students Id
    public bool CheckId(string id)
    {
        return !db.Students.Any(p => p.StudentId == id);
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

    public IActionResult ViewStudent()
    {
        var students = db.Students.ToList();
        return View(students);
    }

    [HttpGet]
    public IActionResult GetStudentData(string searchTerm = "", string sort = "", string dir = "", int page = 1)
    {
        // Base query
        var query = db.Students.AsQueryable();

        // Apply search if provided
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.Trim().ToLower();
            query = query.Where(s => s.StudentId.ToLower().Contains(searchTerm) ||
                                   s.StudentName.ToLower().Contains(searchTerm) ||
                                   s.StudentEmail.ToLower().Contains(searchTerm) ||
                                   s.StudentPhone.ToLower().Contains(searchTerm) ||
                                   s.VerifyStatus.ToLower().Contains(searchTerm));
        }

        // Apply sorting
        if (!string.IsNullOrEmpty(sort))
        {
            switch (sort.ToLower())
            {
                case "id":
                    query = dir == "asc" ? query.OrderBy(s => s.StudentId) : query.OrderByDescending(s => s.StudentId);
                    break;
                case "name":
                    query = dir == "asc" ? query.OrderBy(s => s.StudentName) : query.OrderByDescending(s => s.StudentName);
                    break;
                case "email":
                    query = dir == "asc" ? query.OrderBy(s => s.StudentEmail) : query.OrderByDescending(s => s.StudentEmail);
                    break;
                case "phone":
                    query = dir == "asc" ? query.OrderBy(s => s.StudentPhone) : query.OrderByDescending(s => s.StudentPhone);
                    break;
                case "status":
                    query = dir == "asc" ? query.OrderBy(s => s.VerifyStatus) : query.OrderByDescending(s => s.VerifyStatus);
                    break;
            }
        }

        // Get total count for pagination
        var totalItems = query.Count();
        var pageSize = 10;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        // Apply pagination
        var students = query.Skip((page - 1) * pageSize)
                          .Take(pageSize)
                          .ToList();

        ViewBag.TotalPages = totalPages;
        ViewBag.CurrentPage = page;
        ViewBag.TotalItems = totalItems;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        if (Request.IsAjax())
        {
            return PartialView("_StudentTable", students);
        }

        return View(students);
    }


    // GET: Admin/InsertStudent
    public IActionResult InsertStudent()
    {
        var vm = new StudentVM
        {
            StudentId = NextId(),
        };

        return View(vm);
    }
    // POST: Admin/InsertStudent
    [HttpPost]
    public IActionResult InsertStudent(StudentVM vm)
    {
        if (!ModelState.IsValid)
        {
            vm.StudentId = NextId();
            return View(vm);
        }

        var validationResults = new List<(string field, string error)>();

        // Validate each field individually
        if (!hp.IsValidName(vm.StudentName))
            validationResults.Add(("StudentName", "Invalid student name. Use only letters and spaces."));

        if (!hp.IsValidEmail(vm.StudentEmail))
            validationResults.Add(("StudentEmail", "Invalid email format"));

        if (db.Students.Any(s => s.StudentEmail == vm.StudentEmail))
            validationResults.Add(("StudentEmail", "Email already registered"));

        if (!hp.IsValidPhoneNumber(vm.StudentPhone))
            validationResults.Add(("StudentPhone", "Invalid phone number format(01X-XXXXXXXX)"));

        if (!hp.IsValidPassword(vm.StudentPassword))
            validationResults.Add(("StudentPassword", "Password must be 8+ characters with uppercase, lowercase, number, and special character."));

        if (!hp.IsValidGender(vm.StudentGender))
            validationResults.Add(("StudentGender", "Please select a gender"));

        // Add validation errors to ModelState
        foreach (var (field, errors) in validationResults)
        {
            ModelState.AddModelError(field, errors);
        }

        if (validationResults.Any())
        {
            return View(vm);
        }

        // Photo validation
        var photoError = hp.ValidatePhoto(vm.StudentAvatarURL);
        if (!string.IsNullOrEmpty(photoError))
        {
            ModelState.AddModelError("StudentAvatarURL", photoError);
            return View(vm);
        }

        var student = new Student
        {
            StudentId = NextId(),
            StudentName = vm.StudentName?.Trim().ToUpper() ?? "",
            StudentEmail = vm.StudentEmail?.Trim().ToLower() ?? "",
            StudentPhone = vm.StudentPhone?.Trim() ?? "",
            StudentPassword = hp.HashPassword(vm.StudentPassword),
            VerifyStatus = string.IsNullOrEmpty(vm.VerifyStatus) ? "Pending" : vm.VerifyStatus,
            StudentGender = vm.StudentGender,
            StudentAvatarURL = hp.SavePhoto(vm.StudentAvatarURL, "Students")
        };

        db.Students.Add(student);
        db.SaveChanges();

        TempData["Success"] = "Student added successfully!";
        return RedirectToAction(nameof(ViewStudent));
    
    }
    // GET: Admin/DetailStudents
    public IActionResult DetailStudent(string? id)
    {
        var student = db.Students.Find(id);
        if (student == null)
        {
            return RedirectToAction(nameof(ViewStudent));
        }

        // Create a new instance of PasswordHasher
        //var passwordHasher = new PasswordHasher<object>();

        // Generate a readable password 
        string readablePassword = "********"; // Default mask

        var vm = new StudentUpdateVM
        {
            StudentId = student.StudentId,
            StudentName = student.StudentName,
            StudentEmail = student.StudentEmail,
            StudentPhone = student.StudentPhone,
            StudentGender = student.StudentGender,
            VerifyStatus = student.VerifyStatus,
            StudentPassword = readablePassword, // Use masked password for display
            PhotoURL = student.StudentAvatarURL
        };

        return View(vm);
    }


    // GET: Admin/EditStudent 
    public IActionResult EditStudent(string? id)
    {
        var student = db.Students.Find(id);
        if (student == null)
        {
            TempData["Error"] = "Student not found.";
            return RedirectToAction(nameof(ViewStudent));
        }

        // Create view model with existing data, use masked password
        var vm = new StudentUpdateVM
        {
            StudentId = student.StudentId,
            StudentName = student.StudentName,
            StudentEmail = student.StudentEmail,
            StudentPhone = student.StudentPhone,
            StudentGender = student.StudentGender,
            VerifyStatus = student.VerifyStatus,
            StudentPassword = "********", // Show masked password
            PhotoURL = student.StudentAvatarURL
        };

        return View(vm);
    }

    // POST: Admin/EditStudent 
    [HttpPost]
    public IActionResult EditStudent(StudentUpdateVM vm)
    {
        var student = db.Students.Find(vm.StudentId);
        if (student == null)
        {
            TempData["Error"] = "Student not found.";
            return RedirectToAction(nameof(ViewStudent));
        }

        // Validate name
        if (!hp.IsValidName(vm.StudentName))
        {
            ModelState.AddModelError("StudentName", "Name can only contain letters and spaces");
            vm.PhotoURL = student.StudentAvatarURL;
            return View(vm);
        }

        // Validate email
        if (!hp.IsValidEmail(vm.StudentEmail))
        {
            ModelState.AddModelError("StudentEmail", "Invalid email format");
            vm.PhotoURL = student.StudentAvatarURL;
            return View(vm);
        }

        // Check email uniqueness (excluding current student)
        if (db.Students.Any(s => s.StudentEmail == vm.StudentEmail && s.StudentId != vm.StudentId))
        {
            ModelState.AddModelError("StudentEmail", "Email is already registered");
            vm.PhotoURL = student.StudentAvatarURL;
            return View(vm);
        }

        // Validate phone
        if (!hp.IsValidPhoneNumber(vm.StudentPhone))
        {
            ModelState.AddModelError("StudentPhone", "Invalid phone number format. Use: 01X-XXXXXXXX");
            vm.PhotoURL = student.StudentAvatarURL;
            return View(vm);
        }

        // Validate gender
        if (!hp.IsValidGender(vm.StudentGender))
        {
            ModelState.AddModelError("StudentGender", "Invalid gender selection");
            vm.PhotoURL = student.StudentAvatarURL;
            return View(vm);
        }

        // Only process password if it's different from the mask
        if (!string.IsNullOrEmpty(vm.StudentPassword) && vm.StudentPassword != "********")
        {
            // Validate new password
            if (!hp.IsValidPassword(vm.StudentPassword))
            {
                ModelState.AddModelError("StudentPassword",
                    "Password does not meet requirements");
                vm.PhotoURL = student.StudentAvatarURL;
                return View(vm);
            }
            // Hash and update the new password
            student.StudentPassword = hp.HashPassword(vm.StudentPassword);
        }
        // If password is "********" or empty, keep the existing password
        if (ModelState.IsValid)
        {
            var oldPhotoUrl = student.StudentAvatarURL;
            // Update basic information
            student.StudentName = vm.StudentName.Trim().ToUpper();
            student.StudentEmail = vm.StudentEmail.Trim().ToLower();
            student.StudentPhone = vm.StudentPhone.Trim();
            student.StudentGender = vm.StudentGender;
            student.VerifyStatus = vm.VerifyStatus;

            // Handle photo upload if provided
            if (vm.Photo != null)
            {
                var photoError = hp.ValidatePhoto(vm.Photo);
                if (!string.IsNullOrEmpty(photoError))
                {
                    ModelState.AddModelError("Photo", photoError);
                    vm.PhotoURL = student.StudentAvatarURL;
                    return View(vm);
                }

                // Delete old photo if exists
                if (!string.IsNullOrEmpty(student.StudentAvatarURL))
                {
                    hp.DeletePhoto(student.StudentAvatarURL, "Students");
                }

                // Save new photo
                student.StudentAvatarURL = hp.SavePhoto(vm.Photo, "Students");
            }

            db.SaveChanges();

            SendUserUpdateEmail(
               student.StudentEmail,
               student.StudentName,
               "Student",
               GetAdminId(),
               student.StudentAvatarURL
            );

            TempData["Success"] = "Student has been updated and emailed successfully.";
            return RedirectToAction(nameof(ViewStudent));
        }
        return View(vm);
    }


    // POST: Admin/DeleteStudents
    [HttpPost]
    public IActionResult Delete(string id)
    {
        var student = db.Students.Find(id);
        if (student != null)
        {
            try
            {
                // Delete photo if exists
                if (!string.IsNullOrEmpty(student.StudentAvatarURL))
                {
                    hp.DeletePhoto(student.StudentAvatarURL, "Students");
                }

                db.Students.Remove(student);
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "Student deleted successfully!"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Failed to delete student. Please try again."
                });
            }
        }

        return Json(new
        {
            success = false,
            message = "Student not found or unable to delete."
        });
    }

    //================================================= Tutor ============================================================================================
    public bool CheckTutorId(string id)
    {
        return !db.Tutors.Any(p => p.TutorId == id);
    }

    private string NextTutorId()
    {
        // Get the current max ID
        var maxId = db.Tutors
            .OrderByDescending(s => s.TutorId)
            .FirstOrDefault()?.TutorId ?? "T000";

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
            newId = $"T{nextNumber:D3}";
            nextNumber++;
        } while (db.Tutors.Any(s => s.TutorId == newId));

        return newId;
    }

    //GET: Admin/ViewTutor
    public IActionResult ViewTutor()
    {
        var tutors = db.Tutors.ToList();
        return View(tutors);
    }

    //Get tutor data
    [HttpGet]
    public IActionResult GetTutorData(string searchTerm = "", string sort = "", string dir = "", int page = 1)
    {
        // Base query
        var query = db.Tutors.AsQueryable();

        // Apply search if provided
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.Trim().ToLower();
            query = query.Where(s => s.TutorId.ToLower().Contains(searchTerm) ||
                                   s.TutorName.ToLower().Contains(searchTerm) ||
                                   s.TutorEmail.ToLower().Contains(searchTerm) ||
                                   s.TutorPhone.Contains(searchTerm));
        }

        // Apply sorting
        if (!string.IsNullOrEmpty(sort))
        {
            switch (sort.ToLower())
            {
                case "id":
                    query = dir == "asc" ? query.OrderBy(s => s.TutorId) : query.OrderByDescending(s => s.TutorId);
                    break;
                case "name":
                    query = dir == "asc" ? query.OrderBy(s => s.TutorName) : query.OrderByDescending(s => s.TutorName);
                    break;
                case "email":
                    query = dir == "asc" ? query.OrderBy(s => s.TutorEmail) : query.OrderByDescending(s => s.TutorEmail);
                    break;
                case "phone":
                    query = dir == "asc" ? query.OrderBy(s => s.TutorPhone) : query.OrderByDescending(s => s.TutorPhone);
                    break;
            }
        }

        // Get total count for pagination
        var totalItems = query.Count();
        var pageSize = 5; 
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        // Apply pagination
        var tutors = query.Skip((page - 1) * pageSize)
                         .Take(pageSize)
                         .ToList();

        ViewBag.TotalPages = totalPages;
        ViewBag.CurrentPage = page;
        ViewBag.TotalItems = totalItems;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        return PartialView("_TutorTable", tutors);
    }


    // GET: Admin/InsertTutors
    public IActionResult InsertTutor()
    {
        var vm = new TutorVM
        {
            TutorId = NextTutorId(),
        };

        return View(vm);
    }
    // POST: Admin/InsertTutors
    [HttpPost]
    public IActionResult InsertTutor(TutorVM vm)
    {
        if (!ModelState.IsValid)
        {
            vm.TutorId = NextId();
            return View(vm);
        }

        var validationResults = new List<(string field, string error)>();

        // Validate each field individually
        if (!hp.IsValidName(vm.TutorName))
            validationResults.Add(("TutorName", "Invalid tutor name. Use only letters and spaces."));

        if (!hp.IsValidEmail(vm.TutorEmail))
            validationResults.Add(("TutorEmail", "Invalid email format"));

        if (db.Tutors.Any(s => s.TutorEmail == vm.TutorEmail))
            validationResults.Add(("TutorEmail", "Email already registered"));

        if (!hp.IsValidPhoneNumber(vm.TutorPhone))
            validationResults.Add(("TutorPhone", "Invalid phone number format(01X-XXXXXXXX) "));

        if (!hp.IsValidPassword(vm.TutorPassword))
            validationResults.Add(("TutorPassword", "Password does not meet requirements"));

        if (!hp.IsValidGender(vm.TutorGender))
            validationResults.Add(("TutorGender", "Please select a gender"));

        // Add validation errors to ModelState
        foreach (var (field, errors) in validationResults)
        {
            ModelState.AddModelError(field, errors);
        }

        if (validationResults.Any())
        {
            return View(vm);
        }

        // Photo validation
        var photoError = hp.ValidatePhoto(vm.TutorAvatarURL);
        if (!string.IsNullOrEmpty(photoError))
        {
            ModelState.AddModelError("TutorAvatarURL", photoError);
            return View(vm);
        }

        var tutor = new Tutor
        {
            TutorId = NextTutorId(),
            TutorName = vm.TutorName?.Trim().ToUpper() ?? "",
            TutorEmail = vm.TutorEmail?.Trim().ToLower() ?? "",
            TutorPhone = vm.TutorPhone?.Trim() ?? "",
            TutorPassword = hp.HashPassword(vm.TutorPassword),
            TutorGender = vm.TutorGender,
            TutorAvatarURL = hp.SavePhoto(vm.TutorAvatarURL, "Tutors")
        };

        db.Tutors.Add(tutor);
        db.SaveChanges();

        TempData["Success"] = "Tutor added successfully!";
        return RedirectToAction(nameof(ViewTutor));

    }
    // GET: Admin/DetailTutor
    public IActionResult DetailTutor(string? id)
    {
        var tutor = db.Tutors.Find(id);
        if (tutor == null)
        {
            return RedirectToAction(nameof(ViewTutor));
        }

        // Create masked password for display
        string readablePassword = "********";

        var vm = new TutorUpdateVM
        {
            TutorId = tutor.TutorId,
            TutorName = tutor.TutorName,
            TutorEmail = tutor.TutorEmail,
            TutorPhone = tutor.TutorPhone,
            TutorGender = tutor.TutorGender,
            TutorPassword = readablePassword, // Use masked password for display
            PhotoURL = tutor.TutorAvatarURL,
        };

        return View(vm);
    }


    // GET: Admin/EditTutor
    public IActionResult EditTutor(string? id)
    {
        var tutor = db.Tutors.Find(id);
        if (tutor == null)
        {
            TempData["Error"] = "Tutor not found.";
            return RedirectToAction(nameof(ViewTutor));
        }

        // Create view model with existing data, use masked password
        var vm = new TutorUpdateVM
        {
            TutorId = tutor.TutorId,
            TutorName = tutor.TutorName,
            TutorEmail = tutor.TutorEmail,
            TutorPhone = tutor.TutorPhone,
            TutorGender = tutor.TutorGender,
            TutorPassword = "********", // Show masked password
            PhotoURL = tutor.TutorAvatarURL,
        };

        return View(vm);
    }

    // POST: Admin/Edittutor 
    [HttpPost]
    public IActionResult EditTutor(TutorUpdateVM vm)
    {
        var tutor = db.Tutors.Find(vm.TutorId);
        if (tutor == null)
        {
            TempData["Error"] = "Tutor not found.";
            return RedirectToAction(nameof(ViewTutor));
        }

        // Validate name
        if (!hp.IsValidName(vm.TutorName))
        {
            ModelState.AddModelError("TutorName", "Name can only contain letters and spaces");
            vm.PhotoURL = tutor.TutorAvatarURL;
            return View(vm);
        }

        // Validate email
        if (!hp.IsValidEmail(vm.TutorEmail))
        {
            ModelState.AddModelError("TutorEmail", "Invalid email format");
            vm.PhotoURL = tutor.TutorAvatarURL;
            return View(vm);
        }

        // Check email uniqueness (excluding current tutor)
        if (db.Tutors.Any(t => t.TutorEmail == vm.TutorEmail && t.TutorId != vm.TutorId))
        {
            ModelState.AddModelError("TutorEmail", "Email is already registered");
            vm.PhotoURL = tutor.TutorAvatarURL;
            return View(vm);
        }

        // Validate phone
        if (!hp.IsValidPhoneNumber(vm.TutorPhone))
        {
            ModelState.AddModelError("TutorPhone", "Invalid phone number format. Use: 01X-XXXXXXXX");
            vm.PhotoURL = tutor.TutorAvatarURL;
            return View(vm);
        }

        // Validate gender
        if (!hp.IsValidGender(vm.TutorGender))
        {
            ModelState.AddModelError("TutorGender", "Invalid gender selection");
            vm.PhotoURL = tutor.TutorAvatarURL;
            return View(vm);
        }

        // Password handling
        // Only update password if a new one is provided
        if (!string.IsNullOrEmpty(vm.TutorPassword))
        {
            // If the password is different from the masked password, it's a new password
            if (vm.TutorPassword != "********")
            {
                // Validate new password
                if (!hp.IsValidPassword(vm.TutorPassword))
                {
                    ModelState.AddModelError("TutorPassword",
                        "Password must be at least 8 characters with uppercase, lowercase, number, and special character");
                    vm.PhotoURL = tutor.TutorAvatarURL;
                    return View(vm);
                }
                // Hash and update the new password
                tutor.TutorPassword = hp.HashPassword(vm.TutorPassword);
            }
            // If password is "********", keep the existing password
        }

        // Validate photo if provided
        if (vm.Photo != null)
        {
            var photoError = hp.ValidatePhoto(vm.Photo);
            if (!string.IsNullOrEmpty(photoError))
            {
                ModelState.AddModelError("Photo", photoError);
                vm.PhotoURL = tutor.TutorAvatarURL;
                return View(vm);
            }
        }

        if (ModelState.IsValid)
        {
            // Update basic information
            tutor.TutorName = vm.TutorName.Trim().ToUpper();
            tutor.TutorEmail = vm.TutorEmail.Trim().ToLower();
            tutor.TutorPhone = vm.TutorPhone.Trim();
            tutor.TutorGender = vm.TutorGender;

            // Update photo if provided
            if (vm.Photo != null)
            {
                // Delete old photo
                if (!string.IsNullOrEmpty(tutor.TutorAvatarURL))
                {
                    hp.DeletePhoto(tutor.TutorAvatarURL, "Tutors");
                }

                // Save new photo
                tutor.TutorAvatarURL = hp.SavePhoto(vm.Photo, "Tutors");
            }

            db.SaveChanges();

            SendUserUpdateEmail(
                tutor.TutorEmail,
                tutor.TutorName,
                "Tutor",
                GetAdminId(),
                tutor.TutorAvatarURL
            );

            TempData["Success"] = "Tutor has been updated and emailed successfully.";
            return RedirectToAction(nameof(ViewTutor));
        }
       return View(vm);
    }

    // POST: Admin/DeleteTutor
    [HttpPost]
    public IActionResult DeleteTutor(string id)
    {
        var tutor = db.Tutors.Find(id);
        if (tutor != null)
        {
            // Delete photo if exists
            if (!string.IsNullOrEmpty(tutor.TutorAvatarURL))
            {
                hp.DeletePhoto(tutor.TutorAvatarURL, "Tutors");
            }

            db.Tutors.Remove(tutor);
            db.SaveChanges();

            TempData["Success"] = "Tutor deleted successfully.";
            return Json(new { success = true, message = TempData["Success"] });
        }

        TempData["Error"] = "Tutor not found or unable to delete.";
        return Json(new { success = false, message = TempData["Error"] });
    }
    //================================================= Class ============================================================================================

    // GET: Admin/ViewClass
    public IActionResult ViewClass()
    {
        var classes = db.Classes.Include(c => c.Students).ToList();
        return View(classes);
    }

    // GET: Class data with search, sort and pagination
    [HttpGet]
    public IActionResult GetClassData(string searchTerm = "", string sort = "", string dir = "", int page = 1)
    {
        // Base query with Include for Students
        var query = db.Classes.Include(c => c.Students).AsQueryable();

        // Apply search if provided
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.Trim().ToLower();
            query = query.Where(c => c.ClassId.ToLower().Contains(searchTerm) ||
                                   c.ClassName.ToLower().Contains(searchTerm) ||
                                   c.Status.ToLower().Contains(searchTerm));
        }

        // Apply sorting
        if (!string.IsNullOrEmpty(sort))
        {
            switch (sort.ToLower())
            {
                case "id":
                    query = dir == "asc" ? query.OrderBy(c => c.ClassId) : query.OrderByDescending(c => c.ClassId);
                    break;
                case "name":
                    query = dir == "asc" ? query.OrderBy(c => c.ClassName) : query.OrderByDescending(c => c.ClassName);
                    break;
                case "description":
                    query = dir == "asc" ? query.OrderBy(c => c.ClassDescription) : query.OrderByDescending(c => c.ClassDescription);
                    break;
                case "students":
                    query = dir == "asc" ? query.OrderBy(c => c.Students.Count) : query.OrderByDescending(c => c.Students.Count);
                    break;
                case "status":
                    query = dir == "asc" ? query.OrderBy(c => c.Status) : query.OrderByDescending(c => c.Status);
                    break;
            }
        }

        // Get total count for pagination
        var totalItems = query.Count();
        var pageSize = 5;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        // Apply pagination
        var classes = query.Skip((page - 1) * pageSize)
                          .Take(pageSize)
                          .ToList();

        ViewBag.TotalPages = totalPages;
        ViewBag.CurrentPage = page;
        ViewBag.TotalItems = totalItems;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        return PartialView("_ClassTable", classes);
    }

    // generate next Class ID
    private string NextClassId()
    {
        var maxId = db.Classes
            .OrderByDescending(c => c.ClassId)
            .FirstOrDefault()?.ClassId ?? "C000";

        // Validate the format
        if (maxId.Length < 2 || maxId[0] != 'C')
        {
            maxId = "C000";
        }

        int nextNumber;
        if (int.TryParse(maxId[1..], out int lastNumber))
        {
            nextNumber = lastNumber + 1;
        }
        else
        {
            nextNumber = 1;
        }

        const int MAX_ATTEMPTS = 1000;
        int attempts = 0;
        string newId;

        do
        {
            newId = $"C{nextNumber:D3}";
            nextNumber++;
            attempts++;

            if (attempts >= MAX_ATTEMPTS)
            {
                throw new InvalidOperationException("Unable to generate a unique class ID after maximum attempts");
            }
        } while (db.Classes.Any(c => c.ClassId == newId));

        return newId;
    }

    // GET: Admin/InsertClass
    public IActionResult InsertClass()
    {
        var vm = new ClassVM
        {
            ClassId = NextClassId()
        };
        return View(vm);
    }

    // POST: Admin/InsertClass
    [HttpPost]
    public IActionResult InsertClass(ClassVM vm)
    {
        if (!ModelState.IsValid)
        {
            vm.ClassId = NextClassId();
            return View(vm);
        }

        var validationResults = new List<(string field, string error)>();

        // Validate class name
        if (string.IsNullOrWhiteSpace(vm.ClassName))
            validationResults.Add(("ClassName", "Class name is required"));
        else if (!hp.IsValidName(vm.ClassName))
            validationResults.Add(("ClassName", "Class name can only contain letters and spaces"));

        // Validate description length if provided
        if (!string.IsNullOrWhiteSpace(vm.ClassDescription) && vm.ClassDescription.Length > 255)
            validationResults.Add(("ClassDescription", "Class description cannot exceed 255 characters"));

        // Validate status
        if (string.IsNullOrWhiteSpace(vm.Status))
            validationResults.Add(("Status", "Status is required"));
        else if (!new[] { "Active", "Inactive", "Completed" }.Contains(vm.Status))
            validationResults.Add(("Status", "Invalid status. Must be Active, Inactive, or Completed"));

        // Check for duplicate class name
        if (vm.ClassName != null &&
            db.Classes.Any(c => c.ClassName != null &&
                       c.ClassName.ToLower() == vm.ClassName.ToLower() &&
                       c.ClassId != vm.ClassId))
            validationResults.Add(("ClassName", "Class name already exists"));

        // Add validation errors to ModelState
        foreach (var (field, errors) in validationResults)
        {
            ModelState.AddModelError(field, errors);
        }

        if (validationResults.Any())
        {
            return View(vm);
        }

        var newClass = new Class
        {
            ClassId = NextClassId(),
            ClassName = vm.ClassName.Trim(),
            ClassDescription = vm.ClassDescription?.Trim() ?? "",
            Status = vm.Status.Trim()
        };

        db.Classes.Add(newClass);
        db.SaveChanges();

        TempData["Success"] = "Class added successfully!";
        return RedirectToAction(nameof(ViewClass));
    }

    // GET: Admin/DetailClass
    public IActionResult DetailClass(string? id)
    {
        var classItem = db.Classes.Include(c => c.Students).FirstOrDefault(c => c.ClassId == id);
        if (classItem == null)
        {
            return RedirectToAction(nameof(ViewClass));
        }

        var vm = new ClassVM
        {
            ClassId = classItem.ClassId,
            ClassName = classItem.ClassName,
            ClassDescription = classItem.ClassDescription,
            Status = classItem.Status
        };

        // Add the assigned students to ViewBag
        ViewBag.AssignedStudents = classItem.Students.ToList();

        return View(vm);
    }

    // GET: Admin/EditClass
    public IActionResult EditClass(string? id)
    {
        var classItem = db.Classes.Find(id);
        if (classItem == null)
        {
            TempData["Error"] = "Class not found.";
            return RedirectToAction(nameof(ViewClass));
        }

        var vm = new ClassVM
        {
            ClassId = classItem.ClassId,
            ClassName = classItem.ClassName,
            ClassDescription = classItem.ClassDescription,
            Status = classItem.Status
        };

        return View(vm);
    }

    // POST: Admin/EditClass
    [HttpPost]
    public IActionResult EditClass(ClassVM vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var classItem = db.Classes.Find(vm.ClassId);
        if (classItem == null)
        {
            TempData["Error"] = "Class not found.";
            return RedirectToAction(nameof(ViewClass));
        }

        // Validate name
        if (!hp.IsValidName(vm.ClassName))
        {
            ModelState.AddModelError("ClassName", "Class name can only contain letters and spaces");
            return View(vm);
        }

        // Check name uniqueness (excluding current class)
        if (db.Classes.Any(c => c.ClassName.ToLower() == vm.ClassName.ToLower() && c.ClassId != vm.ClassId))
        {
            ModelState.AddModelError("ClassName", "Class name already exists");
            return View(vm);
        }

        // Validate description length
        if (!string.IsNullOrWhiteSpace(vm.ClassDescription) && vm.ClassDescription.Length > 255)
        {
            ModelState.AddModelError("ClassDescription", "Class description cannot exceed 255 characters");
            return View(vm);
        }

        // Validate status
        if (!new[] { "Active", "Inactive", "Completed" }.Contains(vm.Status))
        {
            ModelState.AddModelError("Status", "Invalid status selection");
            return View(vm);
        }

        // Update basic information
        classItem.ClassName = vm.ClassName.Trim();
        classItem.ClassDescription = vm.ClassDescription?.Trim() ?? "";
        classItem.Status = vm.Status.Trim();

        db.SaveChanges();
        TempData["Success"] = "Class updated successfully.";
        return RedirectToAction(nameof(ViewClass));
    }

    // POST: Admin/DeleteClass
    [HttpPost]
    public IActionResult DeleteClass(string id)
    {
        var classItem = db.Classes.Find(id);
        if (classItem != null)
        {
            // Check if class has any associated students or schedules
            var hasStudents = db.Students.Any(s => s.Classes.Contains(classItem));
            var hasSchedules = db.Schedules.Any(s => s.ClassId == id);

            if (hasStudents || hasSchedules)
            {
                return Json(new { success = false, message = "Cannot delete class as it has associated students or schedules." });
            }

            db.Classes.Remove(classItem);
            db.SaveChanges();

            TempData["Success"] = "Class deleted successfully.";
            return Json(new { success = true, message = TempData["Success"] });
        }

        TempData["Error"] = "Class not found or unable to delete.";
        return Json(new { success = false, message = TempData["Error"] });
    }

    // GET: Admin/AssignStudents
    public IActionResult AssignStudents(string id)
    {
        var classItem = db.Classes.Include(c => c.Students).FirstOrDefault(c => c.ClassId == id);
        if (classItem == null)
        {
            TempData["Error"] = "Class not found.";
            return RedirectToAction(nameof(ViewClass));
        }

        // Get all students
        var allStudents = db.Students.ToList();
        var assignedStudentIds = classItem.Students.Select(s => s.StudentId).ToList();

        ViewBag.Class = classItem;
        ViewBag.AllStudents = allStudents;
        ViewBag.AssignedStudentIds = assignedStudentIds;

        return View();
    }

    // POST: Admin/UpdateStudentAssignments
    [HttpPost]
    public IActionResult UpdateStudentAssignments(string classId, List<string> studentIds)
    {
        var classItem = db.Classes.Include(c => c.Students).FirstOrDefault(c => c.ClassId == classId);
        if (classItem == null)
        {
            return Json(new { success = false, message = "Class not found." });
        }

        // Clear existing assignments
        classItem.Students.Clear();

        if (studentIds != null && studentIds.Any())
        {
            // Add new assignments
            var students = db.Students.Where(s => studentIds.Contains(s.StudentId)).ToList();
            foreach (var student in students)
            {
                classItem.Students.Add(student);
            }
        }

        // Update class status based on student count
        var studentCount = studentIds?.Count ?? 0;
        if (studentCount >= 30) // Assuming 30 is the maximum class size
        {
            classItem.Status = "Full";
        }
        else if (studentCount > 0)
        {
            classItem.Status = "Active";
        }
        else
        {
            classItem.Status = "Inactive";
        }

        db.SaveChanges();
        return Json(new { success = true, message = "Student assignments updated successfully." });
    }
    //================================================= Subject ============================================================================================

    // GET: Admin/ViewSubject
    public IActionResult ViewSubject()
    {
        var subjects = db.Subjects.Include(s => s.Tutors).ToList();
        return View(subjects);
    }

    [HttpGet]
    public IActionResult GetSubjectData(string searchTerm = "", string sort = "", string dir = "", int page = 1)
    {
        // Base query
        var query = db.Subjects.Include(s => s.Tutors).AsQueryable();

        // Apply search if provided
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.Trim().ToLower();
            query = query.Where(s => s.SubjectId.ToLower().Contains(searchTerm) ||
                                   s.SubjectName.ToLower().Contains(searchTerm) ||
                                   s.SubjectDescription.ToLower().Contains(searchTerm));
        }

        // Apply sorting
        if (!string.IsNullOrEmpty(sort))
        {
            switch (sort.ToLower())
            {
                case "id":
                    query = dir == "asc" ? query.OrderBy(s => s.SubjectId) : query.OrderByDescending(s => s.SubjectId);
                    break;
                case "name":
                    query = dir == "asc" ? query.OrderBy(s => s.SubjectName) : query.OrderByDescending(s => s.SubjectName);
                    break;
                case "description":
                    query = dir == "asc" ? query.OrderBy(s => s.SubjectDescription) : query.OrderByDescending(s => s.SubjectDescription);
                    break;
                case "tutors":
                    query = dir == "asc" ? query.OrderBy(s => s.Tutors.Count) : query.OrderByDescending(s => s.Tutors.Count);
                    break;
            }
        }

        // Get total count for pagination
        var totalItems = query.Count();
        var pageSize = 5;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        // Apply pagination
        var subjects = query.Skip((page - 1) * pageSize)
                          .Take(pageSize)
                          .ToList();

        ViewBag.TotalPages = totalPages;
        ViewBag.CurrentPage = page;
        ViewBag.TotalItems = totalItems;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        return PartialView("_SubjectTable", subjects);
    }

    private string NextSubjectId()
    {
        var maxId = db.Subjects
            .OrderByDescending(s => s.SubjectId)
            .FirstOrDefault()?.SubjectId ?? "SUB000";

        if (maxId.Length < 4 || !maxId.StartsWith("SUB"))
        {
            maxId = "SUB000";
        }

        int nextNumber;
        if (int.TryParse(maxId[3..], out int lastNumber))
        {
            nextNumber = lastNumber + 1;
        }
        else
        {
            nextNumber = 1;
        }

        const int MAX_ATTEMPTS = 1000;
        int attempts = 0;
        string newId;

        do
        {
            newId = $"SUB{nextNumber:D3}";
            nextNumber++;
            attempts++;

            if (attempts >= MAX_ATTEMPTS)
            {
                throw new InvalidOperationException("Unable to generate a unique subject ID after maximum attempts");
            }
        } while (db.Subjects.Any(s => s.SubjectId == newId));

        return newId;
    }

    // GET: Admin/InsertSubject
    public IActionResult InsertSubject()
    {
        var vm = new SubjectVM
        {
            SubjectId = NextSubjectId()
        };
        return View(vm);
    }

    // POST: Admin/InsertSubject
    [HttpPost]
    public IActionResult InsertSubject(SubjectVM vm)
    {
        if (!ModelState.IsValid)
        {
            vm.SubjectId = NextSubjectId();
            return View(vm);
        }

        var validationResults = new List<(string field, string error)>();

        // Validate subject name
        if (string.IsNullOrWhiteSpace(vm.SubjectName))
            validationResults.Add(("SubjectName", "Subject name is required"));
        else if (!hp.IsValidName(vm.SubjectName, 100))
            validationResults.Add(("SubjectName", "Subject name can only contain letters and spaces"));

        // Validate subject name length
        if (vm.SubjectName?.Length > 100)
            validationResults.Add(("SubjectName", "Subject name cannot exceed 100 characters"));

        // Check for duplicate subject name
        if (!string.IsNullOrEmpty(vm.SubjectName) &&
            db.Subjects.Any(s => s.SubjectName.ToLower() == vm.SubjectName.ToLower() && s.SubjectId != vm.SubjectId))
            validationResults.Add(("SubjectName", "Subject name already exists"));

        // Validate description length
        if (!string.IsNullOrWhiteSpace(vm.SubjectDescription) && vm.SubjectDescription.Length > 100)
            validationResults.Add(("SubjectDescription", "Description cannot exceed 100 characters"));

        // Add validation errors to ModelState
        foreach (var (field, errors) in validationResults)
        {
            ModelState.AddModelError(field, errors);
        }

        if (validationResults.Any())
        {
            return View(vm);
        }

        var subject = new Subject
        {
            SubjectId = NextSubjectId(),
            SubjectName = vm.SubjectName.Trim(),
            SubjectDescription = vm.SubjectDescription?.Trim() ?? ""
        };

        db.Subjects.Add(subject);
        db.SaveChanges();

        TempData["Success"] = "Subject added successfully!";
        return RedirectToAction(nameof(ViewSubject));
    }

    // GET: Admin/DetailSubject
    public IActionResult DetailSubject(string? id)
    {
        var subject = db.Subjects.Include(s => s.Tutors).FirstOrDefault(s => s.SubjectId == id);
        if (subject == null)
        {
            return RedirectToAction(nameof(ViewSubject));
        }

        var vm = new SubjectVM
        {
            SubjectId = subject.SubjectId,
            SubjectName = subject.SubjectName,
            SubjectDescription = subject.SubjectDescription
        };

        ViewBag.AssignedTutors = subject.Tutors.ToList();
        return View(vm);
    }

    // GET: Admin/EditSubject
    public IActionResult EditSubject(string? id)
    {
        var subject = db.Subjects.Find(id);
        if (subject == null)
        {
            TempData["Error"] = "Subject not found.";
            return RedirectToAction(nameof(ViewSubject));
        }

        var vm = new SubjectVM
        {
            SubjectId = subject.SubjectId,
            SubjectName = subject.SubjectName,
            SubjectDescription = subject.SubjectDescription
        };

        return View(vm);
    }

    // POST: Admin/EditSubject
    [HttpPost]
    public IActionResult EditSubject(SubjectVM vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var subject = db.Subjects.Find(vm.SubjectId);
        if (subject == null)
        {
            TempData["Error"] = "Subject not found.";
            return RedirectToAction(nameof(ViewSubject));
        }

        // Validate name
        if (!hp.IsValidName(vm.SubjectName, 100))
        {
            ModelState.AddModelError("SubjectName", "Subject name can only contain letters and spaces");
            return View(vm);
        }

        // Check name uniqueness (excluding current subject)
        if (db.Subjects.Any(s => s.SubjectName.ToLower() == vm.SubjectName.ToLower() && s.SubjectId != vm.SubjectId))
        {
            ModelState.AddModelError("SubjectName", "Subject name already exists");
            return View(vm);
        }

        // Validate description length
        if (!string.IsNullOrWhiteSpace(vm.SubjectDescription) && vm.SubjectDescription.Length > 100)
        {
            ModelState.AddModelError("SubjectDescription", "Subject description cannot exceed 100 characters");
            return View(vm);
        }

        // Update basic information
        subject.SubjectName = vm.SubjectName.Trim();
        subject.SubjectDescription = vm.SubjectDescription?.Trim() ?? "";

        db.SaveChanges();
        TempData["Success"] = "Subject updated successfully.";
        return RedirectToAction(nameof(ViewSubject));
    }

    // POST: Admin/DeleteSubject
    [HttpPost]
    public IActionResult DeleteSubject(string id)
    {
        var subject = db.Subjects.Find(id);
        if (subject != null)
        {
            // Check if subject has any associated tutors or schedules
            var hasSchedules = db.Schedules.Any(s => s.Subjects.Contains(subject));

            if (hasSchedules)
            {
                return Json(new { success = false, message = "Cannot delete subject as it has associated schedules." });
            }

            db.Subjects.Remove(subject);
            db.SaveChanges();

            TempData["Success"] = "Subject deleted successfully.";
            return Json(new { success = true, message = TempData["Success"] });
        }

        TempData["Error"] = "Subject not found or unable to delete.";
        return Json(new { success = false, message = TempData["Error"] });
    }

    // GET: Admin/AssignTutors
    public IActionResult AssignTutors(string id)
    {
        var subject = db.Subjects.Include(s => s.Tutors).FirstOrDefault(s => s.SubjectId == id);
        if (subject == null)
        {
            TempData["Error"] = "Subject not found.";
            return RedirectToAction(nameof(ViewSubject));
        }

        // Get all tutors
        var allTutors = db.Tutors.ToList();
        var assignedTutorIds = subject.Tutors.Select(t => t.TutorId).ToList();

        ViewBag.Subject = subject;
        ViewBag.AllTutors = allTutors;
        ViewBag.AssignedTutorIds = assignedTutorIds;

        return View();
    }

    // POST: Admin/UpdateTutorAssignments
    [HttpPost]
    public IActionResult UpdateTutorAssignments(string subjectId, List<string> tutorIds)
    {
        var subject = db.Subjects.Include(s => s.Tutors).FirstOrDefault(s => s.SubjectId == subjectId);
        if (subject == null)
        {
            return Json(new { success = false, message = "Subject not found." });
        }

        // Clear existing assignments
        subject.Tutors.Clear();

        if (tutorIds != null && tutorIds.Any())
        {
            // Add new assignments
            var tutors = db.Tutors.Where(t => tutorIds.Contains(t.TutorId)).ToList();
            foreach (var tutor in tutors)
            {
                subject.Tutors.Add(tutor);
            }
        }

        db.SaveChanges();
        return Json(new { success = true, message = "Tutor assignments updated successfully." });
    }


    //================================================= Attendance Reports ============================================================================================

    // GET: Admin/ViewAttendance
    public IActionResult ViewAttendance()
    {
        var attendanceData = db.Attendances
            .Include(a => a.Student)
            .OrderByDescending(a => a.Date)
            .ToList();
        return View(attendanceData);
    }

    // GET: Admin/GetAttendanceData with filtering
    [HttpGet]
    public IActionResult GetAttendanceData(string searchTerm = "", string sort = "", string dir = "",
    DateTime? startDate = null, DateTime? endDate = null, string status = "", int page = 1)
    {
        // Base query with includes for navigation properties
        var query = db.Attendances
            .Include(a => a.Student)
            .Include(a => a.Schedule)
                .ThenInclude(s => s.Class)
            .AsQueryable();

        // Apply date range filter
        if (startDate.HasValue)
        {
            query = query.Where(a => a.Date >= startDate.Value.Date);
        }
        if (endDate.HasValue)
        {
            query = query.Where(a => a.Date <= endDate.Value.Date.AddDays(1));
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.IsPresent == status);
        }

        // Apply search if provided
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.Trim().ToLower();
            query = query.Where(a => a.Student.StudentName.ToLower().Contains(searchTerm) ||
                                    a.Student.StudentId.ToLower().Contains(searchTerm));
        }

        // Apply sorting
        if (!string.IsNullOrEmpty(sort))
        {
            switch (sort.ToLower())
            {
                case "date":
                    query = dir == "asc" ? query.OrderBy(a => a.Date) : query.OrderByDescending(a => a.Date);
                    break;
                case "student":
                    query = dir == "asc" ? query.OrderBy(a => a.Student.StudentName) : query.OrderByDescending(a => a.Student.StudentName);
                    break;
                case "status":
                    query = dir == "asc" ? query.OrderBy(a => a.IsPresent) : query.OrderByDescending(a => a.IsPresent);
                    break;
            }
        }
        else
        {
            // Default sorting by date descending
            query = query.OrderByDescending(a => a.Date);
        }

        // Get total count for pagination
        var totalItems = query.Count();
        var pageSize = 10;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        // Apply pagination and materialize the query
        var attendance = query.Skip((page - 1) * pageSize)
                             .Take(pageSize)
                             .ToList();

        ViewBag.TotalPages = totalPages;
        ViewBag.CurrentPage = page;
        ViewBag.TotalItems = totalItems;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        return PartialView("_AttendanceTable", attendance);
    }

    // GET: Admin/AttendanceReport
    public IActionResult AttendanceReport(string classId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        // Get list of classes for dropdown
        ViewBag.Classes = db.Classes
            .Where(c => c.Status == "Active")
            .Select(c => new { c.ClassId, c.ClassName })
            .ToList();

        if (!startDate.HasValue)
            startDate = DateTime.Today.AddDays(-31).Date; // Set to start of day
        if (!endDate.HasValue)
            endDate = DateTime.Today.Date.AddDays(2).AddSeconds(-1); // Set to end of day


        // First get all students in the class
        var studentsQuery = db.Students
            .Include(s => s.Classes)
            .AsQueryable();

        if (!string.IsNullOrEmpty(classId))
        {
            studentsQuery = studentsQuery.Where(s => s.Classes.Any(c => c.ClassId == classId));
        }

        var students = studentsQuery.ToList();

        // Then get attendance data for these students
        var attendanceData = students.Select(student =>
        {
            var attendances = db.Attendances
                .Include(a => a.Schedule)
                    .ThenInclude(s => s.Class)
                .Where(a => a.StudentId == student.StudentId &&
                           a.Date >= startDate &&
                           a.Date <= endDate &&
                           (classId == null || a.Schedule.ClassId == classId))
                .ToList();

            var className = classId != null
                ? db.Classes.FirstOrDefault(c => c.ClassId == classId)?.ClassName
                : student.Classes.FirstOrDefault()?.ClassName;

            return new AttendanceReportVM
            {
                StudentId = student.StudentId,
                StudentName = student.StudentName,
                ClassId = classId ?? student.Classes.FirstOrDefault()?.ClassId,
                ClassName = className ?? "No Class",
                TotalDays = attendances.Count,
                PresentDays = attendances.Count(a => a.IsPresent == "Present"),
                AbsentDays = attendances.Count(a => a.IsPresent == "Absent"),
                AttendanceRate = attendances.Any()
                    ? (double)attendances.Count(a => a.IsPresent == "Present") / attendances.Count * 100
                    : 0
            };
        })
        .OrderBy(r => r.ClassName)
        .ThenBy(r => r.StudentName)
        .ToList();

        // Calculate daily trends
        var dailyTrendsData = db.Attendances
            .Include(a => a.Schedule)
            .Where(a => a.Date >= startDate &&
                       a.Date <= endDate &&
                       (classId == null || a.Schedule.ClassId == classId))
            .ToList();  // Bring data to memory first

        var dailyTrends = dailyTrendsData
            .GroupBy(a => a.Date.Date)
            .Select(g => new
            {
                Date = g.Key.ToString("MM/dd/yyyy"),
                AttendanceRate = g.Any()
                    ? (double)g.Count(a => a.IsPresent == "Present") / g.Count() * 100
                    : 0
            })
            .OrderBy(x => x.Date)
            .ToList();

        ViewBag.DailyTrends = dailyTrends;
        ViewBag.SelectedClassId = classId;
        ViewBag.StartDate = startDate;
        ViewBag.EndDate = endDate;

        // Calculate overall statistics
        if (attendanceData.Any())
        {
            ViewBag.OverallStats = new
            {
                TotalStudents = students.Count,
                TotalClasses = students.SelectMany(s => s.Classes).Distinct().Count(),
                AverageAttendance = attendanceData.Average(x => x.AttendanceRate),
                PerfectAttendance = attendanceData.Count(x => x.AttendanceRate == 100),
                LowAttendance = attendanceData.Count(x => x.AttendanceRate < 75)
            };
        }

        return View(attendanceData);
    }

    // Export attendance report to Excel/CSV
    public IActionResult ExportAttendanceReport(string classId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = db.Attendances
            .Include(a => a.Student)
            .Include(a => a.Schedule)
                .ThenInclude(s => s.Class)
            .AsQueryable();

        if (!string.IsNullOrEmpty(classId))
        {
            query = query.Where(a => a.Schedule.ClassId == classId);
        }

        if (startDate.HasValue)
            query = query.Where(a => a.Date >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(a => a.Date <= endDate.Value);

        var report = query
            .GroupBy(a => new
            {
                a.Student.StudentId,
                a.Student.StudentName,
                a.Schedule.ClassId,
                a.Schedule.Class.ClassName
            })
            .Select(g => new AttendanceReportVM
            {
                StudentId = g.Key.StudentId,
                StudentName = g.Key.StudentName,
                ClassId = g.Key.ClassId,
                ClassName = g.Key.ClassName,
                TotalDays = g.Count(),
                PresentDays = g.Count(a => a.IsPresent == "Present"),
                AbsentDays = g.Count(a => a.IsPresent == "Absent"),
                AttendanceRate = (double)g.Count(a => a.IsPresent == "Present") / g.Count() * 100
            })
            .OrderBy(r => r.ClassName)
                .ThenByDescending(r => r.AttendanceRate)
            .ToList();

        using (var package = new ExcelPackage())
        {
            var worksheet = package.Workbook.Worksheets.Add("Attendance Report");

            // Add headers
            worksheet.Cells[1, 1].Value = "Class";
            worksheet.Cells[1, 2].Value = "Student ID";
            worksheet.Cells[1, 3].Value = "Student Name";
            worksheet.Cells[1, 4].Value = "Total Days";
            worksheet.Cells[1, 5].Value = "Present Days";
            worksheet.Cells[1, 6].Value = "Absent Days";
            worksheet.Cells[1, 7].Value = "Attendance Rate (%)";

            // Style the header row
            using (var range = worksheet.Cells[1, 1, 1, 7])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                range.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                range.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
            }

            // Add data
            int row = 2;
            string currentClass = "";
            foreach (var item in report)
            {
                if (currentClass != item.ClassName)
                {
                    currentClass = item.ClassName;
                    worksheet.Cells[row, 1, row, 7].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[row, 1, row, 7].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    worksheet.Cells[row, 1].Value = item.ClassName;
                    row++;
                }

                worksheet.Cells[row, 1].Value = item.ClassName;
                worksheet.Cells[row, 2].Value = item.StudentId;
                worksheet.Cells[row, 3].Value = item.StudentName;
                worksheet.Cells[row, 4].Value = item.TotalDays;
                worksheet.Cells[row, 5].Value = item.PresentDays;
                worksheet.Cells[row, 6].Value = item.AbsentDays;
                worksheet.Cells[row, 7].Value = item.AttendanceRate;

                worksheet.Cells[row, 7].Style.Numberformat.Format = "0.0";
                row++;
            }

            // Add summary
            row += 2;
            worksheet.Cells[row, 1].Value = "Report Summary";
            worksheet.Cells[row, 1, row, 7].Merge = true;
            worksheet.Cells[row, 1].Style.Font.Bold = true;

            row++;
            worksheet.Cells[row, 1].Value = "Date Range:";
            worksheet.Cells[row, 2].Value = $"{startDate?.ToString("dd/MM/yyyy") ?? "All time"} - {endDate?.ToString("dd/MM/yyyy") ?? "Present"}";

            row++;
            worksheet.Cells[row, 1].Value = "Total Classes:";
            worksheet.Cells[row, 2].Value = report.Select(r => r.ClassName).Distinct().Count();

            row++;
            worksheet.Cells[row, 1].Value = "Total Students:";
            worksheet.Cells[row, 2].Value = report.Count;

            row++;
            worksheet.Cells[row, 1].Value = "Average Attendance Rate:";
            worksheet.Cells[row, 2].Value = report.Average(r => r.AttendanceRate) / 100;
            worksheet.Cells[row, 2].Style.Numberformat.Format = "0.0%";

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            var content = package.GetAsByteArray();
            var fileName = $"Attendance_Report_{DateTime.Now:yyyyMMdd}.xlsx";

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }

    public IActionResult ExportAttendanceReportPDF(string classId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var report = GenerateAttendanceReport(classId, startDate, endDate);

        using (MemoryStream ms = new MemoryStream())
        {
            Document document = new Document(PageSize.A4, 25, 25, 30, 30);
            PdfWriter writer = PdfWriter.GetInstance(document, ms);
            document.Open();

            // Add title
            var titleFont = FontFactory.GetFont("Helvetica-Bold", 18);
            var title = new Paragraph("Attendance Report", titleFont);
            title.Alignment = Element.ALIGN_CENTER;
            title.SpacingAfter = 20f;
            document.Add(title);

            // Add date range info
            var normalFont = FontFactory.GetFont("Helvetica", 12);
            var dateRange = new Paragraph(
                $"Date Range: {startDate?.ToString("dd/MM/yyyy") ?? "All time"} - {endDate?.ToString("dd/MM/yyyy") ?? "Present"}",
                normalFont
            );
            dateRange.SpacingAfter = 20f;
            document.Add(dateRange);

            // Create table
            PdfPTable table = new PdfPTable(7);
            table.WidthPercentage = 100;

            // Add headers
            string[] headers = { "Class", "Student ID", "Student Name", "Total Days", "Present Days", "Absent Days", "Attendance Rate" };
            foreach (string header in headers)
            {
                var cell = new PdfPCell(new Phrase(header, FontFactory.GetFont("Helvetica-Bold", 12)));
                cell.BackgroundColor = new BaseColor(211, 211, 211); // Light gray
                cell.HorizontalAlignment = Element.ALIGN_CENTER;
                cell.Padding = 5;
                table.AddCell(cell);
            }

            // Add data
            string currentClass = "";
            foreach (var item in report)
            {
                if (currentClass != item.ClassName)
                {
                    currentClass = item.ClassName;
                    var classHeader = new PdfPCell(new Phrase(item.ClassName, FontFactory.GetFont("Helvetica-Bold", 12)));
                    classHeader.Colspan = 7;
                    classHeader.BackgroundColor = new BaseColor(200, 220, 255);
                    classHeader.Padding = 5;
                    table.AddCell(classHeader);
                }

                table.AddCell(new Phrase(item.ClassName, normalFont));
                table.AddCell(new Phrase(item.StudentId, normalFont));
                table.AddCell(new Phrase(item.StudentName, normalFont));
                table.AddCell(new Phrase(item.TotalDays.ToString(), normalFont));
                table.AddCell(new Phrase(item.PresentDays.ToString(), normalFont));
                table.AddCell(new Phrase(item.AbsentDays.ToString(), normalFont));
                table.AddCell(new Phrase($"{item.AttendanceRate:F1}%", normalFont));
            }

            document.Add(table);

            // Add summary
            document.Add(new Paragraph("\nSummary", FontFactory.GetFont("Helvetica-Bold", 14)));
            document.Add(new Paragraph($"Total Classes: {report.Select(r => r.ClassName).Distinct().Count()}", normalFont));
            document.Add(new Paragraph($"Total Students: {report.Count}", normalFont));
            document.Add(new Paragraph($"Overall Average Attendance: {report.Average(r => r.AttendanceRate):F1}%", normalFont));

            document.Close();
            writer.Close();

            var fileContents = ms.ToArray();
            var fileName = $"Attendance_Report_{DateTime.Now:yyyyMMdd}.pdf";
            return File(fileContents, "application/pdf", fileName);
        }
    }

    public IActionResult ExportAttendanceReportCSV(string classId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var report = GenerateAttendanceReport(classId, startDate, endDate);
        var csv = new StringBuilder();

        // Add headers
        csv.AppendLine("Class,Student ID,Student Name,Total Days,Present Days,Absent Days,Attendance Rate");

        foreach (var item in report)
        {
            csv.AppendLine($"{item.ClassName},{item.StudentId},{item.StudentName},{item.TotalDays},{item.PresentDays},{item.AbsentDays},{item.AttendanceRate:F1}%");
        }

        byte[] buffer = Encoding.UTF8.GetBytes(csv.ToString());
        return File(buffer, "text/csv", $"Attendance_Report_{DateTime.Now:yyyyMMdd}.csv");
    }
    //================================================= Schedule ============================================================================================
    // GET: Admin/ViewSchedule
    public IActionResult ViewSchedule()
    {
        var schedules = db.Schedules
            .Include(s => s.Class)
            .Include(s => s.Subjects)
                .ThenInclude(subject => subject.Tutors)
            .OrderBy(s => s.Date)
            .ThenBy(s => s.Time)
            .ToList();

        ViewBag.Classes = db.Classes.Where(c => c.Status == "Active").ToList();
        ViewBag.Subjects = db.Subjects.ToList();
        return View(schedules);
    }

    // GET: Admin/GetScheduleData
    [HttpGet]
    public IActionResult GetScheduleData(string searchTerm = "", DateTime? date = null, string classId = "", string sort = "", string dir = "", int page = 1)
    {
        var query = db.Schedules
            .Include(s => s.Class)
            .Include(s => s.Subjects)
                .ThenInclude(subject => subject.Tutors)
            .AsQueryable();

        // Apply filters
        if (date.HasValue)
        {
            query = query.Where(s => s.Date.Date == date.Value.Date);
        }

        if (!string.IsNullOrEmpty(classId))
        {
            query = query.Where(s => s.ClassId == classId);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            searchTerm = searchTerm.ToLower();
            query = query.Where(s => s.Class.ClassName.ToLower().Contains(searchTerm) ||
                                    s.Subjects.Any(sub => sub.SubjectName.ToLower().Contains(searchTerm)));
        }

        // Apply sorting
        switch (sort?.ToLower())
        {
            case "date":
                query = dir == "asc" ? query.OrderBy(s => s.Date) : query.OrderByDescending(s => s.Date);
                break;
            case "starttime":
                query = dir == "asc" ? query.OrderBy(s => s.StartTime) : query.OrderByDescending(s => s.StartTime);
                break;
            case "endtime":
                query = dir == "asc" ? query.OrderBy(s => s.EndTime) : query.OrderByDescending(s => s.EndTime);
                break;
            case "class":
                query = dir == "asc" ? query.OrderBy(s => s.Class.ClassName) : query.OrderByDescending(s => s.Class.ClassName);
                break;
            default:
                query = query.OrderBy(s => s.Date).ThenBy(s => s.StartTime);
                break;
        }

        var totalItems = query.Count();
        var pageSize = 10;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var schedules = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ViewBag.TotalPages = totalPages;
        ViewBag.CurrentPage = page;
        ViewBag.TotalItems = totalItems;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        return PartialView("_ScheduleTable", schedules);
    }
    //generate 6-digit attendance code
    private string GenerateAttendanceCode()
    {
        // Use current timestamp plus random number
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return (timestamp % 900000 + 100000).ToString();
    }

    //generate Schedule Id
    private string GenerateScheduleId()
    {
        string newId;
        do
        {
            var maxId = db.Schedules
                .OrderByDescending(s => s.ScheduleId)
                .FirstOrDefault()?.ScheduleId ?? "SCH000";

            int nextNumber = int.Parse(maxId.Substring(3)) + 1;
            newId = $"SCH{nextNumber:D3}";
        } while (db.Schedules.Any(s => s.ScheduleId == newId));

        return newId;
    }

    // GET: Admin/InsertSchedule
    public IActionResult InsertSchedule()
    {
        var scheduleId = GenerateScheduleId();
        var attendanceCode = GenerateAttendanceCode();

        Console.WriteLine($"Generated attendance code: {attendanceCode}"); // Debug line

        var vm = new ScheduleVM
        {
            ScheduleId = scheduleId,
            AttendanceCode = attendanceCode
        };

        ViewBag.AttendanceCode = attendanceCode; // Set both VM and ViewBag
        ViewBag.Classes = db.Classes.Where(c => c.Status == "Active").ToList();
        ViewBag.Subjects = db.Subjects.Include(s => s.Tutors).ToList();

        return View(vm);
    }
    // POST: Admin/InsertSchedule
    [HttpPost]
    public IActionResult InsertSchedule(ScheduleVM vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Classes = db.Classes.Where(c => c.Status == "Active").ToList();
            ViewBag.Subjects = db.Subjects.Include(s => s.Tutors).ToList();
            return View(vm);
        }

        var validationResults = new List<(string field, string error)>();

        // Validate date
        if (vm.Date.Date < DateTime.Now.Date)
            validationResults.Add(("Date", "Schedule date cannot be in the past"));

        // Validate time range
        if (vm.Time < TimeSpan.FromHours(8) || vm.Time > TimeSpan.FromHours(17))
            validationResults.Add(("Time", "Class time must be between 8:00 AM and 5:00 PM"));

        // Validate end time
        if (vm.EndTime <= vm.Time)
            validationResults.Add(("EndTime", "End time must be after start time"));

        // Validate duration
        var duration = vm.EndTime - vm.Time;
        if (duration.TotalHours < 1 || duration.TotalHours > 4)
            validationResults.Add(("EndTime", "Class duration must be between 1 and 4 hours"));

        // Validate class selection
        if (string.IsNullOrEmpty(vm.ClassId))
            validationResults.Add(("ClassId", "Class selection is required"));
        else
        {
            var classExists = db.Classes.Any(c => c.ClassId == vm.ClassId && c.Status == "Active");
            if (!classExists)
                validationResults.Add(("ClassId", "Selected class is not available or inactive"));
        }

        // Validate subject selections
        if (vm.SelectedSubjectIds == null || !vm.SelectedSubjectIds.Any())
            validationResults.Add(("SelectedSubjectIds", "At least one subject must be selected"));

        // Validate tutor assignments
        if (vm.SelectedSubjectIds != null)
        {
            foreach (var subjectId in vm.SelectedSubjectIds)
            {
                if (!vm.SubjectTutorAssignments.ContainsKey(subjectId) ||
                    string.IsNullOrEmpty(vm.SubjectTutorAssignments[subjectId]))
                {
                    validationResults.Add(("SubjectTutorAssignments", $"Tutor must be assigned for all selected subjects"));
                    break;
                }
            }
        }

        // Check for scheduling conflicts
        var hasConflict = db.Schedules.Any(s =>
            s.ScheduleId != vm.ScheduleId &&
            s.ClassId == vm.ClassId &&
            s.Date.Date == vm.Date.Date &&
            ((s.Time <= vm.Time && vm.Time < s.EndTime) ||
             (s.Time < vm.EndTime && vm.EndTime <= s.EndTime)));

        if (hasConflict)
            validationResults.Add(("Time", "A schedule already exists for this class at the specified time"));

        // Add validation errors to ModelState
        foreach (var (field, errors) in validationResults)
        {
            ModelState.AddModelError(field, errors);
        }

        if (validationResults.Any())
        {
            ViewBag.Classes = db.Classes.Where(c => c.Status == "Active").ToList();
            ViewBag.Subjects = db.Subjects.Include(s => s.Tutors).ToList();
            return View(vm);
        }

        var schedule = new Schedule
        {
            ScheduleId = GenerateScheduleId(),
            ClassId = vm.ClassId,
            Date = vm.Date,
            Time = vm.Time,
            StartTime = vm.Time,
            EndTime = vm.EndTime,
            AttendanceCode = string.IsNullOrEmpty(vm.AttendanceCode) ?
                            GenerateAttendanceCode() : vm.AttendanceCode
        };

        db.Schedules.Add(schedule);
        db.SaveChanges();

        if (vm.SelectedSubjectIds != null && vm.SelectedSubjectIds.Any())
        {
            foreach (var subjectId in vm.SelectedSubjectIds)
            {
                var subject = db.Subjects.Find(subjectId);
                if (subject != null)
                {
                    schedule.Subjects.Add(subject);
                }
            }
            db.SaveChanges();
        }
        TempData["Success"] = "Schedule added successfully!";
        return RedirectToAction(nameof(ViewSchedule));
    }

    // GET: Admin/EditSchedule
    public IActionResult EditSchedule(string id)
    {
        var schedule = db.Schedules
            .Include(s => s.Subjects)
                .ThenInclude(s => s.Tutors)
            .FirstOrDefault(s => s.ScheduleId == id);

        if (schedule == null)
        {
            TempData["Error"] = "Schedule not found.";
            return RedirectToAction(nameof(ViewSchedule));
        }

        var vm = new ScheduleVM
        {
            ScheduleId = schedule.ScheduleId,
            ClassId = schedule.ClassId,
            Date = schedule.Date,
            Time = schedule.Time,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime,
            AttendanceCode = schedule.AttendanceCode,
            SelectedSubjectIds = schedule.Subjects.Select(s => s.SubjectId).ToList(),
            SubjectTutorAssignments = schedule.Subjects
                .ToDictionary(
                    s => s.SubjectId,
                    s => s.Tutors.FirstOrDefault()?.TutorId ?? ""
                )
        };

        ViewBag.Classes = db.Classes.Where(c => c.Status == "Active").ToList();
        ViewBag.Subjects = db.Subjects.Include(s => s.Tutors).ToList();

        return View(vm);
    }

    // POST: Admin/EditSchedule
    [HttpPost]
    public IActionResult EditSchedule(ScheduleVM vm)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Classes = db.Classes.Where(c => c.Status == "Active").ToList();
            ViewBag.Subjects = db.Subjects.Include(s => s.Tutors).ToList();
            return View(vm);
        }

        var schedule = db.Schedules
            .Include(s => s.Subjects)
            .FirstOrDefault(s => s.ScheduleId == vm.ScheduleId);

        if (schedule == null)
        {
            TempData["Error"] = "Schedule not found.";
            return RedirectToAction(nameof(ViewSchedule));
        }

        // Validate date
        if (vm.Date.Date < DateTime.Now.Date)
        {
            ModelState.AddModelError("Date", "Schedule date cannot be in the past");
            ViewBag.Classes = db.Classes.Where(c => c.Status == "Active").ToList();
            ViewBag.Subjects = db.Subjects.Include(s => s.Tutors).ToList();
            return View(vm);
        }

        // Validate time range
        if (vm.Time < TimeSpan.FromHours(8) || vm.Time > TimeSpan.FromHours(22))
        {
            ModelState.AddModelError("Time", "Class time must be between 8:00 AM and 10:00 PM");
            ViewBag.Classes = db.Classes.Where(c => c.Status == "Active").ToList();
            ViewBag.Subjects = db.Subjects.Include(s => s.Tutors).ToList();
            return View(vm);
        }

        // Validate end time
        if (vm.EndTime <= vm.Time)
        {
            ModelState.AddModelError("EndTime", "End time must be after start time");
            ViewBag.Classes = db.Classes.Where(c => c.Status == "Active").ToList();
            ViewBag.Subjects = db.Subjects.Include(s => s.Tutors).ToList();
            return View(vm);
        }

        // Validate duration
        var duration = vm.EndTime - vm.Time;
        if (duration.TotalHours < 1 || duration.TotalHours > 4)
        {
            ModelState.AddModelError("EndTime", "Class duration must be between 1 and 4 hours");
            ViewBag.Classes = db.Classes.Where(c => c.Status == "Active").ToList();
            ViewBag.Subjects = db.Subjects.Include(s => s.Tutors).ToList();
            return View(vm);
        }

        // Check for schedule conflicts (excluding current schedule)
        var hasConflict = db.Schedules.Any(s =>
            s.ScheduleId != vm.ScheduleId &&
            s.ClassId == vm.ClassId &&
            s.Date.Date == vm.Date.Date &&
            ((s.StartTime <= vm.Time && vm.Time < s.EndTime) ||
             (s.StartTime < vm.EndTime && vm.EndTime <= s.EndTime)));

        if (hasConflict)
        {
            ModelState.AddModelError("Time", "A schedule already exists for this class at the specified time");
            ViewBag.Classes = db.Classes.Where(c => c.Status == "Active").ToList();
            ViewBag.Subjects = db.Subjects.Include(s => s.Tutors).ToList();
            return View(vm);
        }

        // Validate subject selections
        if (vm.SelectedSubjectIds == null || !vm.SelectedSubjectIds.Any())
        {
            ModelState.AddModelError("Subjects", "Please select at least one subject");
            ViewBag.Classes = db.Classes.Where(c => c.Status == "Active").ToList();
            ViewBag.Subjects = db.Subjects.Include(s => s.Tutors).ToList();
            return View(vm);
        }

        // Validate tutor assignments
        foreach (var subjectId in vm.SelectedSubjectIds)
        {
            if (!vm.SubjectTutorAssignments.ContainsKey(subjectId) ||
                string.IsNullOrEmpty(vm.SubjectTutorAssignments[subjectId]))
            {
                ModelState.AddModelError("Tutors", "Please assign a tutor for all selected subjects");
                ViewBag.Classes = db.Classes.Where(c => c.Status == "Active").ToList();
                ViewBag.Subjects = db.Subjects.Include(s => s.Tutors).ToList();
                return View(vm);
            }
        }
        // Update basic schedule information
        schedule.ClassId = vm.ClassId;
        schedule.Date = vm.Date;
        schedule.Time = vm.Time;
        schedule.StartTime = vm.Time;
        schedule.EndTime = vm.EndTime;

        // Update subjects and their tutors
        schedule.Subjects.Clear();
        foreach (var subjectId in vm.SelectedSubjectIds)
        {
            var subject = db.Subjects.Find(subjectId);
            if (subject != null)
            {
                schedule.Subjects.Add(subject);
            }
        }

        db.SaveChanges();
        TempData["Success"] = "Schedule updated successfully.";
        return RedirectToAction(nameof(ViewSchedule));
    }

    // POST: Admin/DeleteSchedule
    [HttpPost]
    public IActionResult DeleteSchedule(string id)
    {
        var schedule = db.Schedules.Find(id);
        if (schedule != null)
        {
            db.Schedules.Remove(schedule);
            db.SaveChanges();
            return Json(new { success = true, message = "Schedule deleted successfully." });
        }
        return Json(new { success = false, message = "Schedule not found." });
    }

    private List<AttendanceReportVM> GenerateAttendanceReport(string classId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = db.Attendances
            .Include(a => a.Student)
            .Include(a => a.Schedule)
                .ThenInclude(s => s.Class)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(classId))
        {
            query = query.Where(a => a.Schedule.ClassId == classId);
        }

        if (startDate.HasValue)
            query = query.Where(a => a.Date >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(a => a.Date <= endDate.Value);

        var report = query
            .GroupBy(a => new
            {
                a.Student.StudentId,
                a.Student.StudentName,
                a.Schedule.ClassId,
                a.Schedule.Class.ClassName
            })
            .Select(g => new AttendanceReportVM
            {
                StudentId = g.Key.StudentId,
                StudentName = g.Key.StudentName,
                ClassId = g.Key.ClassId,
                ClassName = g.Key.ClassName,
                TotalDays = g.Count(),
                PresentDays = g.Count(a => a.IsPresent == "Present"),
                AbsentDays = g.Count(a => a.IsPresent == "Absent"),
                AttendanceRate = (double)g.Count(a => a.IsPresent == "Present") / g.Count() * 100
            })
            .OrderBy(r => r.ClassName)
                .ThenByDescending(r => r.AttendanceRate)
            .ToList();

        return report;
    }

    public IActionResult ScheduleCalendar()
    {
        return View();
    }

    public JsonResult GetScheduleEvents(DateTime start, DateTime end)
    {
        var schedules = db.Schedules
            .Include(s => s.Class)
            .Include(s => s.Subjects)
            .Where(s => s.Date >= start && s.Date <= end)
            .ToList();

        var events = schedules.Select(s => new
        {
            id = s.ScheduleId,
            title = $"{s.Class.ClassName} - {string.Join(", ", s.Subjects.Select(sub => sub.SubjectName))}",
            start = s.Date.Add(s.StartTime).ToString("yyyy-MM-ddTHH:mm:ss"),
            end = s.Date.Add(s.EndTime).ToString("yyyy-MM-ddTHH:mm:ss"),
            className = s.Class.Status == "Active" ? "bg-success" : "bg-warning"
        });

        return Json(events);
    }

}