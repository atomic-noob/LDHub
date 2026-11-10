using LDHub.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Models.ViewModels;
using LDHub.Data;
using LDHub.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LDHub.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly INotificationService _notify;

    public AccountController(
    AppDbContext db,
    IWebHostEnvironment environment,
    INotificationService notify)
{
    _db = db;
    _environment = environment;
    _notify = notify;
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
            return await RegisterFormAsync(model);

        // Email already registered or waiting for approval?
        var emailTaken = await _db.Users.AnyAsync(u => u.Email == model.Email)
                         || await _db.PendingParticipants.AnyAsync(p => p.Email == model.Email);
        if (emailTaken)
        {
            ModelState.AddModelError("Email", "Email already registered or pending approval.");
            return await RegisterFormAsync(model);
        }

        // Profile photo (optional): JPG, PNG or WEBP, up to 5 MB
        string? photoPath = null;
        if (model.Photo != null && model.Photo.Length > 0)
        {
            var photoError = UploadValidator.Validate(
                model.Photo, UploadValidator.ImageExtensions, UploadValidator.ImageMaxBytes);
            if (photoError != null)
            {
                ModelState.AddModelError("Photo", photoError);
                return await RegisterFormAsync(model);
            }

            photoPath = await SaveProfilePhotoAsync(model.Photo);
        }

        var pending = new PendingParticipant
        {
            FirstName     = model.FirstName,
            MiddleName    = model.MiddleName,
            LastName      = model.LastName,
            Email         = model.Email,
            Password      = BCrypt.Net.BCrypt.HashPassword(model.Password),
            ContactNumber = model.ContactNumber,
            Gender        = model.Gender,
            Age           = model.Age,
            Position      = model.Position,
            Designation   = model.Designation,
            SalaryGrade   = model.SalaryGrade,
            RegionId      = model.RegionId,
            OfficeId      = model.OfficeId,
            DivisionId    = model.DivisionId,
            PhotoPath     = photoPath,
            Status        = RecordStatus.Inactive,
            CreatedAt     = DateTime.UtcNow
        };

        _db.PendingParticipants.Add(pending);
        await _db.SaveChangesAsync();

        await NotifyApproversAsync(pending);

        TempData["Success"] = "Registration submitted! Please wait for admin approval.";
        return RedirectToAction("Index", "Admin");
    }

    // Shows the registration form again with its error messages
    private async Task<IActionResult> RegisterFormAsync(RegisterViewModel model)
    {
        ViewBag.Regions = new SelectList(await _db.Regions.ToListAsync(), "RegionId", "RegionName");
        return View(model);
    }

    // Saves the photo under wwwroot/uploads/profile-photos and returns its web path
    private async Task<string> SaveProfilePhotoAsync(IFormFile photo)
    {
        var folder = Path.Combine(_environment.WebRootPath, "uploads", "profile-photos");
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(photo.FileName).ToLowerInvariant()}";
        await using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
        {
            await photo.CopyToAsync(stream);
        }

        return $"/uploads/profile-photos/{fileName}";
    }

    // Tell the RAAM of that region and all Admins that someone is waiting for approval
    private async Task NotifyApproversAsync(PendingParticipant pending)
    {
        var approverIds = await _db.UserRoles
            .Where(r => r.Role == AppRoles.Admin
                     || (r.Role == AppRoles.Raam && r.User != null && r.User.RegionId == pending.RegionId))
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync();

        await _notify.NotifyAsync(approverIds,
            "New registration awaiting approval",
            $"{pending.FirstName} {pending.LastName} has registered and is waiting for approval.",
            Url.Action("PendingParticipants", "Raam"));
    }
}