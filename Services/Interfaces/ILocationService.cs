using System.Collections.Generic;
using System.Threading.Tasks;
using LDHub.Models;

namespace LDHub.Services.Interfaces
{
    // ILocationService defines WHAT the LocationService must do
    // Think of this as a menu — it lists the available methods
    // The actual implementation (logic) is in LocationService.cs
    public interface ILocationService
    {
        // Get all regions ordered by name
        Task<List<Region>> GetAllRegionsAsync();

        // Get offices that belong to a specific region (for cascading dropdown)
        Task<List<Office>> GetOfficesByRegionAsync(int regionId);

        // Get divisions that belong to a specific office (for cascading dropdown)
        Task<List<Division>> GetDivisionsByOfficeAsync(int officeId);
    }
}