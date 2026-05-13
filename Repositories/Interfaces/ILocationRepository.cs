using System.Collections.Generic;
using System.Threading.Tasks;
using LDHub.Models;

namespace LDHub.Repositories.Interfaces
{
    // ILocationRepository defines WHAT DB queries LocationRepository must have
    // All SQL/EF queries for region, office, division tables live in the implementation
    public interface ILocationRepository
    {
        Task<List<Region>> GetAllRegionsAsync();
        Task<List<Office>> GetOfficesByRegionAsync(int regionId);
        Task<List<Division>> GetDivisionsByOfficeAsync(int officeId);
    }
}
