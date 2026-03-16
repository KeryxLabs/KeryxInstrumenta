using AdaptiveCodecContextEngine.Models.Lsp;

namespace AdaptiveCodecContextEngine.Models;

public class AvecCalculator
{
    private readonly AvecWeights _weights;

    public AvecCalculator(AvecWeights weights)
    {
        _weights = weights;
    }

    public AvecScores Calculate(NodeMetrics metrics)
    {
        return new AvecScores
        {
            Stability = CalculateStability(metrics),
            Logic = CalculateLogic(metrics),
            Friction = CalculateFriction(metrics),
            Autonomy = CalculateAutonomy(metrics),
        };
    }

    private double CalculateStability(NodeMetrics m)
    {
        // Churn factor: commits per day (normalized)
        var avgDays = Math.Max(m.GitAvgDaysBetweenChanges, 1);
        var churnFactor = m.GitTotalCommits / avgDays;
        var churnPenalty = Math.Min(churnFactor / _weights.Stability.ChurnNormalize, 1.0);

        // Contributor penalty: more people = more instability
        var contributorPenalty = Math.Min(
            m.GitContributors / (double)_weights.Stability.ContributorCap,
            1.0
        );

        // Test bonus: average of line and branch coverage
        var testBonus = (m.TestLineCoverage / 100.0) * 0.5 + (m.TestBranchCoverage / 100.0) * 0.5;

        // Combine weighted factors
        var stability =
            (1 - churnPenalty * _weights.Stability.ChurnWeight)
            * (1 - contributorPenalty * _weights.Stability.ContributorWeight)
            * (0.5 + testBonus * _weights.Stability.TestWeight);

        return Clamp(stability, 0, 1);
    }

    private double CalculateLogic(NodeMetrics m)
    {
        // Complexity density: complexity per 10 lines of code
        var locNormalized = Math.Max(m.LinesOfCode / (double)_weights.Logic.LocDivisor, 1);
        var complexityDensity = m.CyclomaticComplexity / locNormalized;

        // Parameter weight: normalized by cap
        var parameterWeight = Math.Min(m.Parameters / (double)_weights.Logic.ParameterCap, 1.0);

        // Combine with weights
        var logic =
            complexityDensity * _weights.Logic.ComplexityWeight
            + parameterWeight * _weights.Logic.ParameterWeight;

        return Clamp(logic, 0, 1);
    }

    private double CalculateFriction(NodeMetrics m)
    {
        // Centrality: what portion of total degree is incoming?
        var totalDegree = Math.Max(m.TotalDegree, 1);
        var centrality = m.IncomingEdges / (double)totalDegree;

        // Dependency load: how many things call this (normalized)
        var dependencyLoad = Math.Min(m.IncomingEdges / (double)_weights.Friction.IncomingCap, 1.0);

        // Combine with weights
        var friction =
            centrality * _weights.Friction.CentralityWeight
            + dependencyLoad * _weights.Friction.DependencyWeight;

        return Clamp(friction, 0, 1);
    }

    private double CalculateAutonomy(NodeMetrics m)
    {
        // Autonomy is inverse of dependency ratio
        var totalDegree = Math.Max(m.TotalDegree, 1);
        var dependencyRatio = m.OutgoingEdges / (double)totalDegree;

        var autonomy = 1 - dependencyRatio;

        return Clamp(autonomy, 0, 1);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }
}
