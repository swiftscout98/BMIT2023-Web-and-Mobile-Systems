global using ass.Models;
using Demo;
using Microsoft.AspNetCore.Authentication.Cookies;
OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;
");
builder.Services.AddScoped<Helper>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Auth/Logout";
        //options.AccessDeniedPath = "/Auth/AccessDenied";
        options.AccessDeniedPath = "/Login";
        options.Cookie.Name = "AttendanceSystemCookie";
    });

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// Create Students folder
var uploadsPath = Path.Combine(app.Environment.WebRootPath, "Students");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

// Create Tutors folder
var TutorsuploadsPaths = Path.Combine(app.Environment.WebRootPath, "Tutors");
if (!Directory.Exists(TutorsuploadsPaths))
{
    Directory.CreateDirectory(TutorsuploadsPaths);
}

// Create Admin folder
var adminsUploadPath = Path.Combine(app.Environment.WebRootPath, "Admins");
if (!Directory.Exists(adminsUploadPath))
{
    Directory.CreateDirectory(adminsUploadPath);
}

// Configure routes based on your structure
app.MapControllerRoute(
    name: "login",
    pattern: "Login",
    defaults: new { controller = "Auth", action = "Login" });

app.MapControllerRoute(
    name: "adminArea",
    pattern: "Admin/{action=ViewStart}/{id?}",
    defaults: new { controller = "Admin" });

app.MapControllerRoute(
    name: "studentArea",
    pattern: "Student/{action=ViewStart}/{id?}",
    defaults: new { controller = "Student" });

app.MapControllerRoute(
    name: "tutorArea",
    pattern: "Tutor/{action=ViewStart}/{id?}",
    defaults: new { controller = "Tutor" });

// Default route - redirects to login if not authenticated
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();


app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "0";

    await next();
});

