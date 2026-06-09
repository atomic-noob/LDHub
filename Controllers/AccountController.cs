using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Models.ViewModels;
using LDHub.Data;

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
        ViewBag.Regions = _db.Regions.ToList();
        return View();
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
            ViewBag.Regions = _db.Regions.ToList();
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