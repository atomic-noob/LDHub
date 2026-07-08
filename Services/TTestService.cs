using MathNet.Numerics.Distributions;
using MathNet.Numerics.Statistics;
using LDHub.Models.ViewModels;

namespace LDHub.Services
{
    public static class TTestService
    {
        public static TTestResult? ComputePairedTTest(
            List<double> preScores,
            List<double> postScores,
            string trainingTitle,
            double alpha = 0.05)
        {
            if (preScores.Count != postScores.Count || preScores.Count < 2)
                return null;

            int n = preScores.Count;

            // Differences (Post - Pre)
            var differences = preScores
                .Zip(postScores, (pre, post) => post - pre)
                .ToList();

            double meanDiff = differences.Average();
            double sdDiff   = Statistics.StandardDeviation(differences);
            double seDiff   = sdDiff / Math.Sqrt(n);

            // t-statistic
            double tStat = seDiff != 0 ? meanDiff / seDiff : 0;
            double df    = n - 1;

            // p-value (two-tailed)
            var    tDist  = new StudentT(0, 1, df);
            double pValue = 2.0 * (1.0 - tDist.CumulativeDistribution(Math.Abs(tStat)));

            // Cohen's d
            double cohensD = sdDiff != 0 ? Math.Abs(meanDiff) / sdDiff : 0;

            // 95% CI — t-critical from inverse CDF
            double tCritical = tDist.InverseCumulativeDistribution(0.975);
            double ciLower   = meanDiff - tCritical * seDiff;
            double ciUpper   = meanDiff + tCritical * seDiff;

            // Group stats
            double preMean  = preScores.Average();
            double postMean = postScores.Average();
            double preSD    = Statistics.StandardDeviation(preScores);
            double postSD   = Statistics.StandardDeviation(postScores);
            double preSEM   = preSD  / Math.Sqrt(n);
            double postSEM  = postSD / Math.Sqrt(n);

            // Effect size label
            string effectSize = cohensD < 0.2 ? "Negligible"
                              : cohensD < 0.5 ? "Small"
                              : cohensD < 0.8 ? "Medium"
                              : "Large";

            bool   isSignificant = pValue < alpha;
            string conclusion    = isSignificant
                ? "SIGNIFICANT DIFFERENCE"
                : "NO SIGNIFICANT DIFFERENCE";

            string direction = postMean > preMean ? "improvement" : "decline";
            string narrative = isSignificant
                ? $"A paired samples t-test revealed a statistically significant {direction} from " +
                  $"pre-test scores (M = {preMean:F2}, SD = {preSD:F2}) to post-test scores " +
                  $"(M = {postMean:F2}, SD = {postSD:F2}), t({df:F0}) = {tStat:F4}, p < .05. " +
                  $"Cohen's d = {cohensD:F2} ({effectSize} effect). Null hypothesis REJECTED at α = {alpha}."
                : $"A paired samples t-test was conducted to evaluate the difference between pre-test " +
                  $"and post-test scores for participants of \"{trainingTitle}\". " +
                  $"No statistically significant difference was found between post-test scores " +
                  $"(M = {postMean:F2}, SD = {postSD:F2}) and pre-test scores " +
                  $"(M = {preMean:F2}, SD = {preSD:F2}), t({df:F0}) = {tStat:F4}, p > .05. " +
                  $"Cohen's d = {cohensD:F2} ({effectSize} effect). Null hypothesis NOT rejected at α = {alpha}.";

            return new TTestResult
            {
                N                = n,
                PreMean          = Math.Round(preMean,  4),
                PostMean         = Math.Round(postMean, 4),
                PreSD            = Math.Round(preSD,    4),
                PostSD           = Math.Round(postSD,   4),
                PreSEM           = Math.Round(preSEM,   4),
                PostSEM          = Math.Round(postSEM,  4),
                MeanDifference   = Math.Round(meanDiff, 4),
                TStatistic       = Math.Round(tStat,    4),
                PValue           = Math.Round(pValue,   4),
                DegreesOfFreedom = df,
                CohensD          = Math.Round(cohensD,  4),
                CI_Lower         = Math.Round(ciLower,  4),
                CI_Upper         = Math.Round(ciUpper,  4),
                EffectSize       = effectSize,
                IsSignificant    = isSignificant,
                Conclusion       = conclusion,
                NarrativeResult  = narrative
            };
        }
    }
}