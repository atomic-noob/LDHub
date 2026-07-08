namespace LDHub.Models.ViewModels
{
    public class TTestResult
    {
        public int    N               { get; set; }
        public double PreMean         { get; set; }
        public double PostMean        { get; set; }
        public double PreSD           { get; set; }
        public double PostSD          { get; set; }
        public double PreSEM          { get; set; }
        public double PostSEM         { get; set; }
        public double MeanDifference  { get; set; }
        public double TStatistic      { get; set; }
        public double PValue          { get; set; }
        public double DegreesOfFreedom { get; set; }
        public double CohensD         { get; set; }
        public double CI_Lower        { get; set; }
        public double CI_Upper        { get; set; }
        public string EffectSize      { get; set; } = string.Empty;
        public bool   IsSignificant   { get; set; }
        public string Conclusion      { get; set; } = string.Empty;
        public string NarrativeResult { get; set; } = string.Empty;
    }
}