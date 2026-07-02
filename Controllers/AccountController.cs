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

    public AccountController(AppDbContext db)
    {
        _db = db;
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
            ViewBag.Regions = _db.Regions.ToList();
            return View(model);
        }

        // Check if email already exists in users or pending
        var emailExistsInUsers = await _db.Users
            .AnyAsync(u => u.Email == model.Email);
        var emailExistsInPending = await _db.PendingParticipants
            .AnyAsync(p => p.Email == model.Email);

        if (emailExistsInUsers || emailExistsInPending)
        {
            ModelState.AddModelError("Email", "Email already registered or pending approval.");
            ViewBag.Regions = new SelectList(_db.Regions.ToList(), "RegionId", "RegionName");
            return View(model);
        }

        // Hash password
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);

        // Save to pending_participants
        var pending = new PendingParticipant
        {
            FirstName     = model.FirstName,
            MiddleName    = model.MiddleName,
            LastName      = model.LastName,
            Email         = model.Email,
            Password      = hashedPassword,
            ContactNumber = model.ContactNumber,
            Gender        = model.Gender,
            Age           = model.Age,
            Position      = model.Position,
            Designation   = model.Designation,
            SalaryGrade   = model.SalaryGrade,
            RegionId      = model.RegionId,
            OfficeId      = model.OfficeId,
            DivisionId    = model.DivisionId,
            Status        = "Inactive",
            CreatedAt     = DateTime.UtcNow
        };

        _db.PendingParticipants.Add(pending);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Registration submitted! Please wait for admin approval.";
        return RedirectToAction("Index", "Admin");
    }
}