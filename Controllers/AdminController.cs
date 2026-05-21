using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;

namespace LDHub.Controllers;

public class AdminController : Controller
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    // ── Auth check helper ─────────────────────────────────────────
    private bool IsAdmin() =>
        HttpContext.Session.GetString("UserRole") == "Admin";

    private void SetViewBag()
    {
        ViewBag.ActiveRole = "Admin";
        ViewBag.UserName = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto = HttpContext.Session.GetString("UserPhoto");
    }

    // ── Login ─────────────────────────────────────────────────────
    public IActionResult Index()
    {
        return View("~/Views/Admin/Index.cshtml");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View("~/Views/Admin/Index.cshtml", model);

        // Find user by email
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Email == model.Email);

        // Check if user exists and is Admin
        if (user == null || !user.UserRoles.Any(r => r.Role == "Admin"))
        {
            ViewBag.Error = "Invalid email or password.";
            return View("~/Views/Admin/Index.cshtml", model);
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
        {
            ViewBag.Error = "Invalid email or password.";
            return View("~/Views/Admin/Index.cshtml", model);
        }

        // Set session
        HttpContext.Session.SetString("UserRole", "Admin");
        HttpContext.Session.SetString("UserId", user.UserId.ToString());
        HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
        HttpContext.Session.SetString("UserPhoto", user.Photo ?? "");

        return RedirectToAction("Dashboard", "Admin");
    }
    // GET: /Admin/Hash — TEMPORARY, remove after use


    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Admin");
    }

    // ── Dashboard ─────────────────────────────────────────────────
    public IActionResult Dashboard()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/Dashboard.cshtml");
    }
    public IActionResult Hash()
{
    var hash = BCrypt.Net.BCrypt.HashPassword("Password@123");
    return Content(hash);
}

    // ── Manage Users ──────────────────────────────────────────────
    public IActionResult Employees()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/Employees.cshtml");
    }

    public IActionResult PendingEmployees()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/PendingEmployees.cshtml");
    }

    public IActionResult ResourcePersons()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/ResourcePersons.cshtml");
    }

    // ── Locations ─────────────────────────────────────────────────
    // ── REGIONS CRUD ──────────────────────────────────────────────

// GET: /Admin/Regions — list all regions
public async Task<IActionResult> Regions()
{
    if (!IsAdmin()) return RedirectToAction("Index");
    SetViewBag();
    var regions = await _db.Regions.ToListAsync();
    return View("~/Views/Admin/Regions.cshtml", regions);
}

// POST: /Admin/RegionCreate — create new region
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RegionCreate(Region model)
{
    if (!IsAdmin()) return RedirectToAction("Index");

    // Check for duplicate region name
    var exists = await _db.Regions
        .AnyAsync(r => r.RegionName == model.RegionName);
    if (exists)
    {
        TempData["Error"] = "Region already exists!";
        return RedirectToAction("Regions");
    }

    _db.Regions.Add(model);
    await _db.SaveChangesAsync();
    TempData["Success"] = "Region added successfully!";
    return RedirectToAction("Regions");
}

// POST: /Admin/RegionEdit — update existing region
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RegionEdit(Region model)
{
    if (!IsAdmin()) return RedirectToAction("Index");

    var region = await _db.Regions.FindAsync(model.RegionId);
    if (region == null)
    {
        TempData["Error"] = "Region not found!";
        return RedirectToAction("Regions");
    }

    region.RegionName = model.RegionName;
    region.RegionCode = model.RegionCode;
    await _db.SaveChangesAsync();
    TempData["Success"] = "Region updated successfully!";
    return RedirectToAction("Regions");
}

// POST: /Admin/RegionDelete — delete region
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RegionDelete(int id)
{
    if (!IsAdmin()) return RedirectToAction("Index");

    var region = await _db.Regions.FindAsync(id);
    if (region == null)
    {
        TempData["Error"] = "Region not found!";
        return RedirectToAction("Regions");
    }

    _db.Regions.Remove(region);
    await _db.SaveChangesAsync();
    TempData["Success"] = "Region deleted successfully!";
    return RedirectToAction("Regions");
}
    // ── OFFICES CRUD ──────────────────────────────────────────────

// GET: /Admin/Offices — list all offices with regions
public async Task<IActionResult> Offices()
{
    if (!IsAdmin()) return RedirectToAction("Index");
    SetViewBag();

    // Load offices with their region names (like LEFT JOIN in PHP)
    var offices = await _db.Offices
        .Include(o => o.Region)
        .OrderBy(o => o.OfficeName)
        .ToListAsync();

    // Pass regions to view for dropdown
    ViewBag.Regions = await _db.Regions
        .OrderBy(r => r.RegionName)
        .ToListAsync();

    return View("~/Views/Admin/Offices.cshtml", offices);
}

// POST: /Admin/OfficeCreate — create new office
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> OfficeCreate(Office model)
{
    if (!IsAdmin()) return RedirectToAction("Index");

    // Check duplicate office name in same region
    var exists = await _db.Offices
        .AnyAsync(o => o.OfficeName == model.OfficeName 
                    && o.RegionId == model.RegionId);
    if (exists)
    {
        TempData["Error"] = "Office already exists in this region!";
        return RedirectToAction("Offices");
    }

    _db.Offices.Add(model);
    await _db.SaveChangesAsync();
    TempData["Success"] = "Office added successfully!";
    return RedirectToAction("Offices");
}

// POST: /Admin/OfficeBulkCreate — bulk add preset offices
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> OfficeBulkCreate(int regionId, 
    List<string> officeNames, List<string> officeCodes)
{
    if (!IsAdmin()) return RedirectToAction("Index");

    int added = 0;
    for (int i = 0; i < officeNames.Count; i++)
    {
        // Skip if already exists
        var exists = await _db.Offices
            .AnyAsync(o => o.OfficeName == officeNames[i] 
                        && o.RegionId == regionId);
        if (exists) continue;

        _db.Offices.Add(new Office
        {
            OfficeName = officeNames[i],
            OfficeCode = officeCodes.Count > i ? officeCodes[i] : null,
            RegionId   = regionId
        });
        added++;
    }

    await _db.SaveChangesAsync();
    TempData["Success"] = $"{added} office(s) added successfully!";
    return RedirectToAction("Offices");
}

// POST: /Admin/OfficeEdit — update existing office
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> OfficeEdit(Office model)
{
    if (!IsAdmin()) return RedirectToAction("Index");

    var office = await _db.Offices.FindAsync(model.OfficeId);
    if (office == null)
    {
        TempData["Error"] = "Office not found!";
        return RedirectToAction("Offices");
    }

    office.OfficeName = model.OfficeName;
    office.OfficeCode = model.OfficeCode;
    office.RegionId   = model.RegionId;
    await _db.SaveChangesAsync();
    TempData["Success"] = "Office updated successfully!";
    return RedirectToAction("Offices");
}

// POST: /Admin/OfficeDelete — delete office
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> OfficeDelete(int id)
{
    if (!IsAdmin()) return RedirectToAction("Index");

    var office = await _db.Offices.FindAsync(id);
    if (office == null)
    {
        TempData["Error"] = "Office not found!";
        return RedirectToAction("Offices");
    }

    _db.Offices.Remove(office);
    await _db.SaveChangesAsync();
    TempData["Success"] = "Office deleted successfully!";
    return RedirectToAction("Offices");
}

// ── DIVISIONS CRUD ────────────────────────────────────────────

// GET: /Admin/Divisions — list all divisions
public async Task<IActionResult> Divisions()
{
    if (!IsAdmin()) return RedirectToAction("Index");
    SetViewBag();

    // Load divisions with office and region names
    var divisions = await _db.Divisions
        .Include(d => d.Office)
            .ThenInclude(o => o.Region)
        .OrderBy(d => d.DivisionName)
        .ToListAsync();

    // Pass regions for dropdown
    ViewBag.Regions = await _db.Regions
        .OrderBy(r => r.RegionName)
        .ToListAsync();

    return View("~/Views/Admin/Divisions.cshtml", divisions);
}

// POST: /Admin/DivisionCreate — create single division
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DivisionCreate(Division model)
{
    if (!IsAdmin()) return RedirectToAction("Index");

    var exists = await _db.Divisions
        .AnyAsync(d => d.DivisionName == model.DivisionName 
                    && d.OfficeId == model.OfficeId);
    if (exists)
    {
        TempData["Error"] = "Division already exists in this office!";
        return RedirectToAction("Divisions");
    }

    _db.Divisions.Add(model);
    await _db.SaveChangesAsync();
    TempData["Success"] = "Division added successfully!";
    return RedirectToAction("Divisions");
}

// POST: /Admin/DivisionBulkCreate — bulk add divisions
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DivisionBulkCreate(int officeId,
    List<string> divisionNames, List<string> divisionCodes)
{
    if (!IsAdmin()) return RedirectToAction("Index");

    int added = 0;
    for (int i = 0; i < divisionNames.Count; i++)
    {
        var exists = await _db.Divisions
            .AnyAsync(d => d.DivisionName == divisionNames[i] 
                        && d.OfficeId == officeId);
        if (exists) continue;

        _db.Divisions.Add(new Division
        {
            DivisionName = divisionNames[i],
            DivisionCode = divisionCodes.Count > i ? divisionCodes[i] : null,
            OfficeId     = officeId
        });
        added++;
    }

    await _db.SaveChangesAsync();
    TempData["Success"] = $"{added} division(s) added successfully!";
    return RedirectToAction("Divisions");
}

// POST: /Admin/DivisionEdit — update division
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DivisionEdit(Division model)
{
    if (!IsAdmin()) return RedirectToAction("Index");

    var division = await _db.Divisions.FindAsync(model.DivisionId);
    if (division == null)
    {
        TempData["Error"] = "Division not found!";
        return RedirectToAction("Divisions");
    }

    division.DivisionName = model.DivisionName;
    division.DivisionCode = model.DivisionCode;
    division.OfficeId     = model.OfficeId;
    await _db.SaveChangesAsync();
    TempData["Success"] = "Division updated successfully!";
    return RedirectToAction("Divisions");
}

// POST: /Admin/DivisionDelete — delete division
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DivisionDelete(int id)
{
    if (!IsAdmin()) return RedirectToAction("Index");

    var division = await _db.Divisions.FindAsync(id);
    if (division == null)
    {
        TempData["Error"] = "Division not found!";
        return RedirectToAction("Divisions");
    }

    _db.Divisions.Remove(division);
    await _db.SaveChangesAsync();
    TempData["Success"] = "Division deleted successfully!";
    return RedirectToAction("Divisions");
}

// ── AJAX: Get offices by region (for cascading dropdowns) ─────
[HttpGet]
public async Task<IActionResult> GetOfficesByRegion(int regionId)
{
    var offices = await _db.Offices
        .Where(o => o.RegionId == regionId)
        .OrderBy(o => o.OfficeName)
        .Select(o => new { o.OfficeId, o.OfficeName, o.OfficeCode })
        .ToListAsync();
    return Json(offices);
}

    // ── Trainings ─────────────────────────────────────────────────
    public IActionResult Trainings()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/Trainings.cshtml");
    }

    public IActionResult FreeTrainings()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/FreeTrainings.cshtml");
    }

    // ── Announcements ─────────────────────────────────────────────
    public IActionResult Announcements()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/Announcements.cshtml");
    }

    // ── Reports ───────────────────────────────────────────────────
    public IActionResult ReportUsers()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/ReportUsers.cshtml");
    }

    public IActionResult ReportIdp()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/ReportIdp.cshtml");
    }

    public IActionResult ReportTraining()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/ReportTraining.cshtml");
    }

    // ── FAQs ──────────────────────────────────────────────────────
    public IActionResult Faqs()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/Faqs.cshtml");
    }
}