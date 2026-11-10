using LDHub.Models;
using LDHub.Models.ViewModels;

namespace LDHub.Services.Interfaces;

public interface ITrainingReportService
{
    /// <summary>Builds the pre/post test report (scores, pass rate, paired T-test) for a training.</summary>
    Task<TrainingReportViewModel> BuildAsync(Training training);
}
