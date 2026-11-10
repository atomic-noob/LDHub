namespace LDHub.Models.ViewModels;

public class SaveProgressRequest
{
    public int TrainingId { get; set; }
    public int? ModuleId { get; set; }
    public string TestType { get; set; } = string.Empty;
    public Dictionary<string, string?>? Answers { get; set; }
}
