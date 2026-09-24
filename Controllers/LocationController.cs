using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using LDHub.Services.Interfaces;
using LDHub.Models;
using LDHub.Data;
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
            return role == "Admin" || role == "Raam";
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
        public async Task<IActionResult> Regions()
        {
            if (!CanManageLocations()) return RedirectToAction("Index", "Home");
            SetViewBag();
            var regions = await _db.Regions.ToListAsync();
            return View("~/Views/Location/Regions.cshtml", regions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegionCreate(Region model)
        {
            if (!CanManageLocations()) return RedirectToAction("Index", "Home");

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
        public async Task<IActionResult> RegionEdit(Region model)
        {
            if (!CanManageLocations()) return RedirectToAction("Index", "Home");

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
        public async Task<IActionResult> RegionDelete(int id)
        {
            if (!CanManageLocations()) return RedirectToAction("Index", "Home");

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
        public async Task<IActionResult> Offices()
        {
            if (!CanManageLocations()) return RedirectToAction("Index", "Home");
            SetViewBag();

            var offices = await _db.Offices
                .Include(o => o.Region)
                .OrderBy(o => o.OfficeName)
                .ToListAsync();

            ViewBag.Regions = await _db.Regions.OrderBy(r => r.RegionName).ToListAsync();

            return View("~/Views/Location/Offices.cshtml", offices);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OfficeCreate(Office model)
        {
            if (!CanManageLocations()) return RedirectToAction("Index", "Home");

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
        public async Task<IActionResult> OfficeBulkCreate(int regionId,
            List<string> officeNames, List<string> officeCodes)
        {
            if (!CanManageLocations()) return RedirectToAction("Index", "Home");

            int added = 0;
            for (int i = 0; i < officeNames.Count; i++)
            {
                var exists = await _db.Offices
                    .AnyAsync(o => o.OfficeName == officeNames[i] && o.RegionId == regionId);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OfficeEdit(Office model)
        {
            if (!CanManageLocations()) return RedirectToAction("Index", "Home");

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OfficeDelete(int id)
        {
            if (!CanManageLocations()) return RedirectToAction("Index", "Home");

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

        // ═══════════════════════════════════════
        //  DIVISIONS CRUD
        // ═══════════════════════════════════════
        public async Task<IActionResult> Divisions()
        {
            if (!CanManageLocations()) return RedirectToAction("Index", "Home");
            SetViewBag();

            var divisions = await _db.Divisions
                .Include(d => d.Office)
                    .ThenInclude(o => o.Region)
                .OrderBy(d => d.DivisionName)
                .ToListAsync();

            ViewBag.Regions = await _db.Regions.OrderBy(r => r.RegionName).ToListAsync();

            return View("~/Views/Location/Divisions.cshtml", divisions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DivisionCreate(Division model)
        {
            if (!CanManageLocations()) return RedirectToAction("Index", "Home");

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
        public async Task<IActionResult> DivisionBulkCreate(int officeId,
            List<string> divisionNames, List<string> divisionCodes)
        {
            if (!CanManageLocations()) return RedirectToAction("Index", "Home");

            int added = 0;
            for (int i = 0; i < divisionNames.Count; i++)
            {
                var exists = await _db.Divisions
                    .AnyAsync(d => d.DivisionName == divisionNames[i] && d.OfficeId == officeId);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DivisionEdit(Division model)
        {
            if (!CanManageLocations()) return RedirectToAction("Index", "Home");

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DivisionDelete(int id)
        {
            if (!CanManageLocations()) return RedirectToAction("Index", "Home");

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

        // ── AJAX: used by Offices/Divisions management pages ──
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
    }
}