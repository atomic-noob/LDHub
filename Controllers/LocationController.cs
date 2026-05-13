using System.Linq;
using System.Threading.Tasks;
using LDHub.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LDHub.Controllers
{
    // LocationController handles AJAX requests for cascading dropdowns
    // Called from the registration form when user selects region/office
    // Equivalent to fetch_offices.php and fetch_divisions.php in PHP
    public class LocationController : Controller
    {
        private readonly ILocationService _locationService;

        public LocationController(ILocationService locationService)
        {
            _locationService = locationService;
        }

        // GET: /Location/GetOffices?regionId=1
        // Called via AJAX when user selects a region in registration form
        // Returns JSON list of offices for that region
        [HttpGet]
        public async Task<IActionResult> GetOffices(int regionId)
        {
            var offices = await _locationService.GetOfficesByRegionAsync(regionId);

            // Return JSON array matching what the JS expects
            // { value: officeId, text: "Office Name (CODE)" }
            var result = offices.Select(o => new
            {
                value = o.OfficeId,
                text = string.IsNullOrEmpty(o.OfficeCode)
                    ? o.OfficeName
                    : $"{o.OfficeName} ({o.OfficeCode})"
            });

            return Json(result);
        }

        // GET: /Location/GetDivisions?officeId=1
        // Called via AJAX when user selects an office in registration form
        // Returns JSON list of divisions for that office
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
    }
}