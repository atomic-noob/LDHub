namespace LDHub.Models.ViewModels;

public class DiscussionBoardViewModel
{
    public Training Training { get; set; } = null!;
    public bool CanAnnounce { get; set; }
    public bool IsStaff { get; set; }
    public int CurrentUserId { get; set; }
    public List<DiscussionThread> Threads { get; set; } = new();
}

public class DiscussionThread
{
    public TrainingPost Post { get; set; } = null!;
    public List<TrainingPost> Replies { get; set; } = new();
}
