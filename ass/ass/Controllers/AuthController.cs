using Demo;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using System.Collections.Concurrent;

public class AuthController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;
    private static readonly ConcurrentDictionary<string, LoginAttemptInfo> LoginAttempts = new();

    private const int MaxFailedAttempts = 4;
    private readonly TimeSpan BlockDuration = TimeSpan.FromSeconds(30);

    public AuthController(DB db, IWebHostEnvironment en, Helper hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    public class LoginAttemptInfo
    {
        public int FailedAttempts { get; set; }
        public DateTime? BlockedUntil { get; set; }
    }

    // GET: Tutor/Index
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Login(LoginVM vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        string prefix = vm.UserId[0].ToString().ToUpper();
        string redirectUrl = null;
        string userType = null;

        switch (prefix)
        {
            case "A":
                if (ValidateAdmin(vm))
                {
                    redirectUrl = "/admin/dashboard";
                    userType = "Admin";
                }
                break;
            case "T":
                if (ValidateTutor(vm))
                {
                    redirectUrl = "/tutor/dashboard";
                    userType = "Tutor";
                }
                break;
            case "S":
                if (IsUserBlocked(vm.UserId))
                {
                    ModelState.AddModelError("", "Your account is temporarily locked. Please try again later.");
                    return View(vm);
                }
                if (ValidateStudent(vm))
                {
                    redirectUrl = "/student/index";
                    userType = "Student";
                }
                break;
            default:
                ModelState.AddModelError("", "Invalid ID prefix");
                return View(vm);
        }

        if (redirectUrl != null)
        {
            TempData["UserType"] = userType;
            //return Redirect(redirectUrl);
            return Json(new { redirectUrl });

        }

        return View(vm);
    }

    private bool ValidateStudent(LoginVM vm)
    {
        var student = db.Students.FirstOrDefault(s => s.StudentId == vm.UserId);
        if (student != null)
        {
            if (student.VerifyStatus == "Pending")
            {
                ModelState.AddModelError("", "Your account is pending verification. Please wait for approval.");
                return false;
            }

            var passwordHasher = new PasswordHasher<LoginVM>();
            var passwordVerificationResult = passwordHasher.VerifyHashedPassword(vm, student.StudentPassword, vm.Password);
            if (passwordVerificationResult == PasswordVerificationResult.Success)
            {
                ResetLoginAttempts(vm.UserId);
                var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, student.StudentId),
                new Claim(ClaimTypes.Name, student.StudentEmail),
                new Claim("FullName", student.StudentName),
                new Claim(ClaimTypes.Role, "Student")
            };
                SignInUser(claims);
                TempData["LoginRole"] = "Student";
                return true;
            }
        }

        RegisterFailedAttempt(vm.UserId);
        ModelState.AddModelError("", "Invalid ID or Password");
        return false;
    }

    private bool ValidateAdmin(LoginVM vm)
    {
        var admin = db.Admins.FirstOrDefault(a => a.AdminId == vm.UserId);
        if (admin != null)
        {
            var passwordHasher = new PasswordHasher<LoginVM>();
            var passwordVerificationResult = passwordHasher.VerifyHashedPassword(vm, admin.AdminPassword, vm.Password);
            if (passwordVerificationResult == PasswordVerificationResult.Success)
            {
                var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, admin.AdminId),
                new Claim(ClaimTypes.Name, admin.AdminName),
                new Claim(ClaimTypes.Role, "Admin")
            };
                SignInUser(claims);
                TempData["LoginRole"] = "Admin"; 
                TempData["Success"] = "Login successfully.";
                return true;
            }
        }
        ModelState.AddModelError("", "Admin Invalid ID or Password");
        return false;
    }


    private bool ValidateTutor(LoginVM vm)
    {
        var tutor = db.Tutors.FirstOrDefault(t => t.TutorId == vm.UserId);
        if (tutor != null)
        {
            var passwordHasher = new PasswordHasher<LoginVM>();
            var passwordVerificationResult = passwordHasher.VerifyHashedPassword(vm, tutor.TutorPassword, vm.Password);
            if (passwordVerificationResult == PasswordVerificationResult.Success)
            {
                var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, tutor.TutorId),
                new Claim(ClaimTypes.Name, tutor.TutorName),
                new Claim(ClaimTypes.Role, "Tutor")
            };
                SignInUser(claims);
                TempData["LoginRole"] = "Tutor"; 
                return true;
            }
        }
        ModelState.AddModelError("", "Tutor Invalid ID or Password");
        return false;
    }


    private bool IsUserBlocked(string userId)
    {
        if (LoginAttempts.TryGetValue(userId, out var attemptInfo))
        {
            if (attemptInfo.BlockedUntil.HasValue && attemptInfo.BlockedUntil > DateTime.UtcNow)
            {
                return true; // User is currently blocked
            }

            if (attemptInfo.BlockedUntil.HasValue && attemptInfo.BlockedUntil <= DateTime.UtcNow)
            {
                LoginAttempts.TryRemove(userId, out _); // Unblock user if the block duration has expired
            }
        }
        return false;
    }

    private void ResetLoginAttempts(string userId)
    {
        LoginAttempts.TryRemove(userId, out _); // Clear the user's failed attempts
    }

    private void RegisterFailedAttempt(string userId)
    {
        var attemptInfo = LoginAttempts.GetOrAdd(userId, _ => new LoginAttemptInfo { FailedAttempts = 0 });

        // If the user has already reached the maximum attempts, block them immediately
        if (attemptInfo.FailedAttempts >= MaxFailedAttempts)
        {
            attemptInfo.BlockedUntil = DateTime.UtcNow.Add(BlockDuration);
            return;
        }

        attemptInfo.FailedAttempts++;

        // Block the user if they just reached the maximum attempts
        if (attemptInfo.FailedAttempts >= MaxFailedAttempts)
        {
            attemptInfo.BlockedUntil = DateTime.UtcNow.Add(BlockDuration);
        }
    }

    private void SignInUser(List<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal).Wait();
    }

    public IActionResult Logout(string? returnURL)
    {
        TempData["PlayLogoutSound"] = true;
        hp.SignOut();
        return RedirectToAction("Login", "Auth");
    }
}