namespace LDHub.Models;
 
public class GradeRow
{
    public int     UserId          { get; set; }
    public string  EmployeeName    { get; set; } = string.Empty;
 
    public decimal? PreTestScore   { get; set; }   // null = not taken
    public decimal? PostTestScore  { get; set; }   // best of 3, null = not taken
    public decimal  TotalPoints    { get; set; }
 
    public int     PostTestAttempts { get; set; }  // 0–3
    public bool    PostTestPassed   { get; set; }
}
 