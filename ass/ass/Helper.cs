using ass.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Net.Mail;
using System.Net;
using System.Text.RegularExpressions;
using Image = SixLabors.ImageSharp.Image;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Demo;

public class Helper
{
    private readonly IWebHostEnvironment en;
    private readonly DB db;
    private readonly IConfiguration cf;
    private readonly IHttpContextAccessor ct;

    public Helper(IWebHostEnvironment en, DB db, IConfiguration cf, IHttpContextAccessor ct)
    {
        this.en = en;
        this.db = db;
        this.cf = cf;
        this.ct = ct;
    }
    public bool IsValidIdFormat(string id, string expectedPrefix)
    {
        if (string.IsNullOrEmpty(id) || id.Length != 4)
            return false;

        return id.StartsWith(expectedPrefix) &&
               int.TryParse(id.Substring(1), out int number) &&
               number >= 1 &&
               number <= 999;
    }

    // ------------------------------------------------------------------------
    // Account 
    // ------------------------------------------------------------------------
    public void SignOut()
    {
        ct.HttpContext!.SignOutAsync();
    }

    private PasswordHasher<object> ph = new();

    public string HashPassword(string password)
    {
        return ph.HashPassword(0, password);
    }

    public bool VerifyPassword(string hash, string password)
    {
        return ph.VerifyHashedPassword(0, hash, password)
               == PasswordVerificationResult.Success;
    }
    public void SendEmail(MailMessage mail)
    {
        string user = cf["Smtp:User"] ?? "";
        string pass = cf["Smtp:Pass"] ?? "";
        string name = cf["Smtp:Name"] ?? "";
        string host = cf["Smtp:Host"] ?? "";
        int port = cf.GetValue<int>("Smtp:Port");

        mail.From = new MailAddress(user, name);

        using var smtp = new SmtpClient
        {
            Host = host,
            Port = port,
            EnableSsl = true,
            Credentials = new NetworkCredential(user, pass),
        };

        smtp.Send(mail);
    }

    public string RandomPassword()
    {
        string s = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string password = "";

        Random r = new();

        for (int i = 1; i <= 10; i++)
        {
            password += s[r.Next(s.Length)];
        }

        return password;
    }

    // ------------------------------------------------------------------------
    // Photo 
    // ------------------------------------------------------------------------
    public string ValidatePhoto(IFormFile f)
    {
        var reType = new Regex(@"^image\/(jpeg|png)$", RegexOptions.IgnoreCase);
        var reName = new Regex(@"^.+\.(jpeg|jpg|png)$", RegexOptions.IgnoreCase);

        if (!reType.IsMatch(f.ContentType) || !reName.IsMatch(f.FileName))
        {
            return "Only JPG and PNG photo is allowed.";
        }
        else if (f.Length > 1 * 2048 * 2048)
        {
            return "Photo size cannot more than 1MB.";
        }

        return "";
    }

    public string SavePhoto(IFormFile f, string folder)
    {
        var file = Guid.NewGuid().ToString("n") + ".jpg";
        var path = Path.Combine(en.WebRootPath, folder, file);

        var options = new ResizeOptions
        {
            Size = new(200, 200),
            Mode = ResizeMode.Crop,
        };

        using var stream = f.OpenReadStream();
        using var img = Image.Load(stream);
        img.Mutate(x => x.Resize(options));
        img.Save(path);

        return file;
    }

    public void DeletePhoto(string file, string folder)
    {
        if (string.IsNullOrEmpty(file)) return;
        SafeDeleteFile(file, Path.Combine(en.WebRootPath, folder), file);
    }
    public static void SafeDeleteFile(string filePath, string baseFolder, string fileName)
    {
        // Security: Extract just the filename to prevent path traversal
        fileName = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(fileName))
            return;

        // Construct the full path safely
        string fullPath = Path.Combine(baseFolder, fileName);
        if (!File.Exists(fullPath))
            return;

        int maxRetries = 3;
        int retryDelayMs = 100;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                // Ensure any file handles are released using GC
                GC.Collect();
                GC.WaitForPendingFinalizers();

                File.Delete(fullPath);
                break; // If successful, exit the loop
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                // Wait before trying again
                Thread.Sleep(retryDelayMs);
            }
        }
    }
    // ------------------------------------------------------------------------
    // DateTime Helper Functions
    // ------------------------------------------------------------------------

    // Return January (1) to December (12)
    public SelectList GetMonthList()
    {
        var list = new List<object>();

        for (int n = 1; n <= 12; n++)
        {
            list.Add(new
            {
                Id = n,
                Name = new DateTime(1, n, 1).ToString("MMMM"),
            });
        }

        return new SelectList(list, "Id", "Name");
    }

    // Return min to max years
    public SelectList GetYearList(int min, int max, bool reverse = false)
    {
        var list = new List<int>();

        for (int n = min; n <= max; n++)
        {
            list.Add(n);
        }

        if (reverse) list.Reverse();

        return new SelectList(list);
    }

    // ------------------------------------------------------------------------
    // Validations
    // ------------------------------------------------------------------------
    public bool IsValidEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
            return false;

        // Regular expression for basic email validation
        string pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
        Regex regex = new Regex(pattern);

        // Check if the email matches the basic structure
        if (!regex.IsMatch(email))
        {
            return false;
        }

        try
        {
            // Ensure it passes the System.Net.Mail.MailAddress check
            var addr = new System.Net.Mail.MailAddress(email);

            // Validate domain for special characters or invalid patterns
            string domain = addr.Host;
            if (domain.Contains("#") || domain.Contains("!"))
            {
                return false;
            }

            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    public bool IsValidPhoneNumber(string phone)
    {
        if (string.IsNullOrEmpty(phone)) return false;
        return Regex.IsMatch(phone, @"^(\+?6?01)[02-46-9][-][0-9]{7}$|^(\+?6?01)[1][-][0-9]{8}$");
    }

    public bool IsValidPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;

        var hasNumber = new Regex(@"[0-9]+");
        var hasUpperChar = new Regex(@"[A-Z]+");
        var hasLowerChar = new Regex(@"[a-z]+");
        var hasSpecialChar = new Regex(@"[!@#$%^&*(),.?"":{}|<>]+");

        return password.Length >= 8 &&
               hasNumber.IsMatch(password) &&
               hasUpperChar.IsMatch(password) &&
               hasLowerChar.IsMatch(password) &&
               hasSpecialChar.IsMatch(password);
    }

    public bool IsValidName(string name, int maxLength = 50)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return name.Length <= maxLength &&
               name.All(c => char.IsLetter(c) || char.IsWhiteSpace(c));
    }

    public bool IsValidGender(string gender)
    {
        return gender == "M" || gender == "F";
    }
}