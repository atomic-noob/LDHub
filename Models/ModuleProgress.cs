// Models/ModuleProgress.cs
namespace LDHub.Models;

public class ModuleProgress
{
    public int Id          { get; set; }
    public int UserId      { get; set; }
    public int ModuleId    { get; set; }
    public int TrainingId  { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public User           User     { get; set; } = null!;
    public TrainingModule Module   { get; set; } = null!;
    public Training       Training { get; set; } = null!;
}