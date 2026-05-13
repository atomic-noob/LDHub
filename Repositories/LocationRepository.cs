using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LDHub.Data;
using LDHub.Models;
using LDHub.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Repositories
{
    // LocationRepository handles ALL database queries for location tables
    // This is the ONLY place that talks to the DB for location data
    // No business logic here — just pure DB queries
    public class LocationRepository : ILocationRepository
    {
        private readonly AppDbContext _context;

        // AppDbContext is injected automatically by .NET (like including config.php)
        public LocationRepository(AppDbContext context)
        {
            _context = context;
        }

        // Get all regions ordered by name
        // Equivalent to: SELECT * FROM region ORDER BY region_name
        public async Task<List<Region>> GetAllRegionsAsync()
        {
            return await _context.Regions
                .OrderBy(r => r.RegionName)
                .ToListAsync();
        }

        // Get offices for a specific region (cascading dropdown)
        // Equivalent to: SELECT * FROM office WHERE region_id = ? ORDER BY office_name
        public async Task<List<Office>> GetOfficesByRegionAsync(int regionId)
        {
            return await _context.Offices
                .Where(o => o.RegionId == regionId)
                .OrderBy(o => o.OfficeName)
                .ToListAsync();
        }

        // Get divisions for a specific office (cascading dropdown)
        // Equivalent to: SELECT * FROM division WHERE office_id = ? ORDER BY division_name
        public async Task<List<Division>> GetDivisionsByOfficeAsync(int officeId)
        {
            return await _context.Divisions
                .Where(d => d.OfficeId == officeId)
                .OrderBy(d => d.DivisionName)
                .ToListAsync();
        }
    }
}