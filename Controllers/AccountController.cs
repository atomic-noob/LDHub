using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Models.ViewModels;
using LDHub.Data;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LDHub.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public AccountController(
    AppDbContext db,
    IWebHostEnvironment environment)
{
    _db = db;
    _environment = environment;
}
    // GET: /Account/Register
        public IActionResult Register()
        {
            ViewBag.Regions = new SelectList(_db.Regions.ToList(), "RegionId", "RegionName");
            return View();
        }
        // GET: /Account/GetOffices?regionId=1
            [HttpGet]
            public IActionResult GetOffices(int regionId)
            {
                var offices = _db.Offices
                    .Where(o => o.RegionId == regionId)
                    .Select(o => new { value = o.OfficeId, text = o.OfficeName })
                    .ToList();
                return Json(offices);
            }

            // GET: /Account/GetDivisions?officeId=1
            [HttpGet]
            public IActionResult GetDivisions(int officeId)
            {
                var divisions = _db.Divisions
                    .Where(d => d.OfficeId == officeId)
                    .Select(d => new { value = d.DivisionId, text = d.DivisionName })
                    .ToList();
                return Json(divisions);
            }

    // POST: /Account/Register
   [HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Register(RegisterViewModel model)
{
    if (!ModelState.IsValid)
    {
        ViewBag.Regions = new SelectList(
            _db.Regions.ToList(),
            "RegionId",
            "RegionName"
        );

        return View(model);
    }

    // Check if email already exists
    var emailExistsInUsers = await _db.Users
        .AnyAsync(u => u.Email == model.Email);

    var emailExistsInPending = await _db.PendingParticipants
        .AnyAsync(p => p.Email == model.Email);

    if (emailExistsInUsers || emailExistsInPending)
    {
        ModelState.AddModelError(
            "Email",
            "Email already registered or pending approval."
        );

        ViewBag.Regions = new SelectList(
            _db.Regions.ToList(),
            "RegionId",
            "RegionName"
        );

        return View(model);
    }

    // ─────────────────────────────────────────
    // PROFILE PHOTO UPLOAD
    // ─────────────────────────────────────────
    string? photoPath = null;

    if (model.Photo != null && model.Photo.Length > 0)
    {
        // Maximum 5 MB
        const long maxFileSize = 5 * 1024 * 1024;

        if (model.Photo.Length > maxFileSize)
        {
            ModelState.AddModelError(
                "Photo",
                "Profile photo must not exceed 5 MB."
            );

            ViewBag.Regions = new SelectList(
                _db.Regions.ToList(),
                "RegionId",
                "RegionName"
            );

            return View(model);
        }

        // Allowed extensions
        var allowedExtensions = new[]
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        var extension = Path
            .GetExtension(model.Photo.FileName)
            .ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            ModelState.AddModelError(
                "Photo",
                "Only JPG, JPEG, PNG, and WEBP images are allowed."
            );

            ViewBag.Regions = new SelectList(
                _db.Regions.ToList(),
                "RegionId",
                "RegionName"
            );

            return View(model);
        }

        // wwwroot/uploads/profile-photos
        var uploadFolder = Path.Combine(
            _environment.WebRootPath,
            "uploads",
            "profile-photos"
        );

        // Create directory if it doesn't exist
        if (!Directory.Exists(uploadFolder))
        {
            Directory.CreateDirectory(uploadFolder);
        }

        // Generate unique filename
        var fileName =
            $"{Guid.NewGuid():N}{extension}";

        var physicalPath = Path.Combine(
            uploadFolder,
            fileName
        );

        using (var stream = new FileStream(
            physicalPath,
            FileMode.Create))
        {
            await model.Photo.CopyToAsync(stream);
        }

        // Store web path in database
        photoPath =
            $"/uploads/profile-photos/{fileName}";
    }

    // Hash password
    var hashedPassword =
        BCrypt.Net.BCrypt.HashPassword(model.Password);

    var pending = new PendingParticipant
    {
        FirstName = model.FirstName,
        MiddleName = model.MiddleName,
        LastName = model.LastName,

        Email = model.Email,
        Password = hashedPassword,

        ContactNumber = model.ContactNumber,
        Gender = model.Gender,
        Age = model.Age,

        Position = model.Position,
        Designation = model.Designation,
        SalaryGrade = model.SalaryGrade,

        RegionId = model.RegionId,
        OfficeId = model.OfficeId,
        DivisionId = model.DivisionId,

        // NEW
        PhotoPath = photoPath,

        Status = "Inactive",
        CreatedAt = DateTime.UtcNow
    };

    _db.PendingParticipants.Add(pending);

    await _db.SaveChangesAsync();

    TempData["Success"] =
        "Registration submitted! Please wait for admin approval.";

    return RedirectToAction("Index", "Admin");
}
}