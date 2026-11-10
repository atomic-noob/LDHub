using LDHub.Models.ViewModels;

namespace LDHub.Services.Interfaces;

public interface IAdminDashboardService
{
    Task<AdminDashboardData> GetAsync(DateTime now);
}
