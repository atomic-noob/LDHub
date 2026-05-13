using System.Collections.Generic;
using System.Threading.Tasks;
using LDHub.Models;
using LDHub.Repositories.Interfaces;
using LDHub.Services.Interfaces;

namespace LDHub.Services
{
    // LocationService is the business logic layer for location data
    // It calls LocationRepository to get data, then returns it to the controller
    // For location data there isn't much business logic — mostly just passing through
    // but this pattern keeps the code clean and testable
    public class LocationService : ILocationService
    {
        private readonly ILocationRepository _locationRepository;

        public LocationService(ILocationRepository locationRepository)
        {
            _locationRepository = locationRepository;
        }

        public async Task<List<Region>> GetAllRegionsAsync()
        {
            return await _locationRepository.GetAllRegionsAsync();
        }

        public async Task<List<Office>> GetOfficesByRegionAsync(int regionId)
        {
            return await _locationRepository.GetOfficesByRegionAsync(regionId);
        }

        public async Task<List<Division>> GetDivisionsByOfficeAsync(int officeId)
        {
            return await _locationRepository.GetDivisionsByOfficeAsync(officeId);
        }
    }
}