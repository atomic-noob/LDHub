using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using LDHub.Services.Interfaces;
using LDHub.Models;
using LDHub.Data;
using LDHub.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Controllers
{
    public class LocationController : Controller
    {
        private readonly ILocationService _locationService;
        private readonly AppDbContext _db;

        public LocationController(ILocationService locationService, AppDbContext db)
        {
            _locationService = locationService;
            _db = db;
        }

        // ── Access check — Admin AND Raam can manage locations ──
        private bool CanManageLocations()
        {
            var role = HttpContext.Session.GetString("UserRole");
            return role == AppRoles.Admin || role == AppRoles.Raam;
        }

        private void SetViewBag()
        {
            ViewBag.ActiveRole = HttpContext.Session.GetString("UserRole");
            ViewBag.UserName   = HttpContext.Session.GetString("UserName");
        }

        // ═══════════════════════════════════════
        //  EXISTING AJAX (registration form) — unchanged
        // ═══════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> GetOffices(int regionId)
        {
            var offices = await _locationService.GetOfficesByRegionAsync(regionId);
            var result = offices.Select(o => new
            {
                value = o.OfficeId,
                text = string.IsNullOrEmpty(o.OfficeCode)
                    ? o.OfficeName
                    : $"{o.OfficeName} ({o.OfficeCode})"
            });
            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetDivisions(int officeId)
        {
            var divisions = await _locationService.GetDivisionsByOfficeAsync(officeId);
            var result = divisions.Select(d => new
            {
                value = d.DivisionId,
                text = string.IsNullOrEmpty(d.DivisionCode)
                    ? d.DivisionName
                    : $"{d.DivisionName} ({d.DivisionCode})"
            });
            return Json(result);
        }

        // ═══════════════════════════════════════
        //  REGIONS CRUD
        // ═══════════════════════════════════════
        [RequireRole(AppRoles.Admin, AppRoles.Raam, RedirectAction = "Index", RedirectController = "Home")]
        public async Task<IActionResult> Regions()
        {
            SetViewBag();
            var regions = await _db.Regions.ToListAsync();
            return View("~/Views/Location/Regions.cshtml", regions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRole(AppRoles.Admin, AppRoles.Raam, RedirectAction = "Index", RedirectController = "Home")]
        public async Task<IActionResult> RegionCreate(Region model)
        {

            var exists = await _db.Regions.AnyAsync(r => r.RegionName == model.RegionName);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRole(AppRoles.Admin, AppRoles.Raam, RedirectAction = "Index", RedirectController = "Home")]
        public async Task<IActionResult> RegionEdit(Region model)
        {

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRole(AppRoles.Admin, AppRoles.Raam, RedirectAction = "Index", RedirectController = "Home")]
        public async Task<IActionResult> RegionDelete(int id)
        {

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

        // ═══════════════════════════════════════
        //  OFFICES CRUD
        // ═══════════════════════════════════════
       [RequireRole(AppRoles.Admin, AppRoles.Raam, RedirectAction = "Index", RedirectController = "Home")]
       public async Task<IActionResult> Offices()
            {
                SetViewBag();

                var role     = HttpContext.Session.GetString("UserRole");
                var regionId = await GetRaamRegionId();

                IQueryable<Office> query = _db.Offices.Include(o => o.Region);

                if (role == AppRoles.Raam)
                    query = query.Where(o => o.RegionId == regionId);

                var offices = await query.OrderBy(o => o.OfficeName).ToListAsync();

                // RAAM only gets their own region in the dropdown — prevents picking another region
                ViewBag.Regions = role == AppRoles.Raam
                    ? await _db.Regions.Where(r => r.RegionId == regionId).ToListAsync()
                    : await _db.Regions.OrderBy(r => r.RegionName).ToListAsync();

                ViewBag.IsRaam = role == AppRoles.Raam;

                return View("~/Views/Location/Offices.cshtml", offices);
            }

      [HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin, AppRoles.Raam, RedirectAction = "Index", RedirectController = "Home")]
public async Task<IActionResult> OfficeCreate(Office model)
{

    var role     = HttpContext.Session.GetString("UserRole");
    var regionId = await GetRaamRegionId();

    if (role == AppRoles.Raam)
        model.RegionId = regionId ?? 0; // force their own region regardless of what was submitted

    if (model.RegionId == 0)
    {
        TempData["Error"] = "Please select a region!";
        return RedirectToAction("Offices");
    }

    var exists = await _db.Offices
        .AnyAsync(o => o.OfficeName == model.OfficeName && o.RegionId == model.RegionId);
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
      [HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin, AppRoles.Raam, RedirectAction = "Index", RedirectController = "Home")]
public async Task<IActionResult> OfficeBulkCreate(int regionId,
    List<string> officeNames, List<string> officeCodes)
{

    var role         = HttpContext.Session.GetString("UserRole");
    var raamRegionId = await GetRaamRegionId();

    if (role == AppRoles.Raam && regionId != raamRegionId)
    {
        TempData["Error"] = "You can only add offices to your own region.";
        return RedirectToAction("Offices");
    }

    // One query for the offices that already exist in this region
    var existingOffices = (await _db.Offices
        .Where(o => o.RegionId == regionId)
        .Select(o => o.OfficeName)
        .ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);

    int added = 0;
    for (int i = 0; i < officeNames.Count; i++)
    {
        if (string.IsNullOrWhiteSpace(officeNames[i])) continue;
        if (!existingOffices.Add(officeNames[i])) continue;   // already exists (or repeated in this batch)

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
        [HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin, AppRoles.Raam, RedirectAction = "Index", RedirectController = "Home")]
public async Task<IActionResult> OfficeEdit(Office model)
{

    var office = await _db.Offices.FindAsync(model.OfficeId);
    if (office == null)
    {
        TempData["Error"] = "Office not found!";
        return RedirectToAction("Offices");
    }

    var role     = HttpContext.Session.GetString("UserRole");
    var regionId = await GetRaamRegionId();

    if (role == AppRoles.Raam && office.RegionId != regionId)
    {
        TempData["Error"] = "You do not have access to this office.";
        return RedirectToAction("Offices");
    }

    office.OfficeName = model.OfficeName;
    office.OfficeCode = model.OfficeCode;
    office.RegionId   = role == AppRoles.Raam ? regionId ?? office.RegionId : model.RegionId;
    await _db.SaveChangesAsync();
    TempData["Success"] = "Office updated successfully!";
    return RedirectToAction("Offices");
}

[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin, AppRoles.Raam, RedirectAction = "Index", RedirectController = "Home")]
public async Task<IActionResult> OfficeDelete(int id)
{

    var office = await _db.Offices.FindAsync(id);
    if (office == null)
    {
        TempData["Error"] = "Office not found!";
        return RedirectToAction("Offices");
    }

    var role     = HttpContext.Session.GetString("UserRole");
    var regionId = await GetRaamRegionId();

    if (role == AppRoles.Raam && office.RegionId != regionId)
    {
        TempData["Error"] = "You do not have access to this office.";
        return RedirectToAction("Offices");
    }

    _db.Offices.Remove(office);
    await _db.SaveChangesAsync();
    TempData["Success"] = "Office deleted successfully!";
    return RedirectToAction("Offices");
}

        // ═══════════════════════════════════════
        //  DIVISIONS CRUD
        // ═══════════════════════════════════════
      [RequireRole(AppRoles.Admin, AppRoles.Raam, RedirectAction = "Index", RedirectController = "Home")]
      public async Task<IActionResult> Divisions()
{
    SetViewBag();

    var role     = HttpContext.Session.GetString("UserRole");
    var regionId = await GetRaamRegionId();

    IQueryable<Division> query = _db.Divisions
        .Include(d => d.Office).ThenInclude(o => o.Region);

    if (role == AppRoles.Raam)
        query = query.Where(d => d.Office.RegionId == regionId);

    var divisions = await query.OrderBy(d => d.DivisionName).ToListAsync();

    ViewBag.Regions = role == AppRoles.Raam
        ? await _db.Regions.Where(r => r.RegionId == regionId).ToListAsync()
        : await _db.Regions.OrderBy(r => r.RegionName).ToListAsync();

    ViewBag.IsRaam = role == AppRoles.Raam;

    return View("~/Views/Location/Divisions.cshtml", divisions);
}

[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin, AppRoles.Raam, RedirectAction = "Index", RedirectController = "Home")]
public async Task<IActionResult> DivisionCreate(Division model)
{

    var role     = HttpContext.Session.GetString("UserRole");
    var regionId = await GetRaamRegionId();

    if (role == AppRoles.Raam)
    {
        var office = await _db.Offices.FindAsync(model.OfficeId);
        if (office == null || office.RegionId != regionId)
        {
            TempData["Error"] = "You can only add divisions to offices in your region.";
            return RedirectToAction("Divisions");
        }
    }

    var exists = await _db.Divisions
        .AnyAsync(d => d.DivisionName == model.DivisionName && d.OfficeId == model.OfficeId);
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

[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin, AppRoles.Raam, RedirectAction = "Index", RedirectController = "Home")]
public async Task<IActionResult> DivisionEdit(Division model)
{

    var division = await _db.Divisions.Include(d => d.Office).FirstOrDefaultAsync(d => d.DivisionId == model.DivisionId);
    if (division == null)
    {
        TempData["Error"] = "Division not found!";
        return RedirectToAction("Divisions");
    }

    var role     = HttpContext.Session.GetString("UserRole");
    var regionId = await GetRaamRegionId();

    if (role == AppRoles.Raam && division.Office?.RegionId != regionId)
    {
        TempData["Error"] = "You do not have access to this division.";
        return RedirectToAction("Divisions");
    }

    division.DivisionName = model.DivisionName;
    division.DivisionCode = model.DivisionCode;
    division.OfficeId     = model.OfficeId;
    await _db.SaveChangesAsync();
    TempData["Success"] = "Division updated successfully!";
    return RedirectToAction("Divisions");
}

[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin, AppRoles.Raam, RedirectAction = "Index", RedirectController = "Home")]
public async Task<IActionResult> DivisionDelete(int id)
{

    var division = await _db.Divisions.Include(d => d.Office).FirstOrDefaultAsync(d => d.DivisionId == id);
    if (division == null)
    {
        TempData["Error"] = "Division not found!";
        return RedirectToAction("Divisions");
    }

    var role     = HttpContext.Session.GetString("UserRole");
    var regionId = await GetRaamRegionId();

    if (role == AppRoles.Raam && division.Office?.RegionId != regionId)
    {
        TempData["Error"] = "You do not have access to this division.";
        return RedirectToAction("Divisions");
    }

    _db.Divisions.Remove(division);
    await _db.SaveChangesAsync();
    TempData["Success"] = "Division deleted successfully!";
    return RedirectToAction("Divisions");
}

       [HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin, AppRoles.Raam, RedirectAction = "Index", RedirectController = "Home")]
public async Task<IActionResult> DivisionBulkCreate(int officeId,
    List<string> divisionNames, List<string> divisionCodes)
{

    var role         = HttpContext.Session.GetString("UserRole");
    var raamRegionId = await GetRaamRegionId();

    if (role == AppRoles.Raam)
    {
        var office = await _db.Offices.FindAsync(officeId);
        if (office == null || office.RegionId != raamRegionId)
        {
            TempData["Error"] = "You can only add divisions to offices in your own region.";
            return RedirectToAction("Divisions");
        }
    }

    // One query for the divisions that already exist in this office
    var existingDivisions = (await _db.Divisions
        .Where(d => d.OfficeId == officeId)
        .Select(d => d.DivisionName)
        .ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);

    int added = 0;
    for (int i = 0; i < divisionNames.Count; i++)
    {
        if (string.IsNullOrWhiteSpace(divisionNames[i])) continue;
        if (!existingDivisions.Add(divisionNames[i])) continue;   // already exists (or repeated in this batch)

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


        // ── AJAX: used by Offices/Divisions management pages ──
       [HttpGet]
public async Task<IActionResult> GetOfficesByRegion(int regionId)
{
    var role      = HttpContext.Session.GetString("UserRole");
    var raamRegionId = await GetRaamRegionId();

    if (role == AppRoles.Raam && regionId != raamRegionId)
        return Json(new List<object>()); // return empty, block cross-region lookups

    var offices = await _db.Offices
        .Where(o => o.RegionId == regionId)
        .OrderBy(o => o.OfficeName)
        .Select(o => new { o.OfficeId, o.OfficeName, o.OfficeCode })
        .ToListAsync();
    return Json(offices);
}
        private async Task<int?> GetRaamRegionId()
            {
                var role = HttpContext.Session.GetString("UserRole");
                if (role != AppRoles.Raam) return null;

                var userIdStr = HttpContext.Session.GetString("UserId");
                if (!int.TryParse(userIdStr, out int userId)) return null;

                var user = await _db.Users.FindAsync(userId);
                return user?.RegionId;
            }


    }
}