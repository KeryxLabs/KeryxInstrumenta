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
        // 1. Structural Friction (LSP) - How "Central" is this?
        var totalDegree = Math.Max(m.TotalDegree, 1);
        var centrality = m.IncomingEdges / (double)totalDegree;
        var dependencyLoad = Math.Min(m.IncomingEdges / (double)_weights.Friction.IncomingCap, 1.0);

        var structuralFriction = (centrality * 0.4) + (dependencyLoad * 0.6);

        // 2. Process Friction (Git) - Is this a "Hotspot"?
        // High commits + Many contributors = High coordination friction.
        var churn = Math.Min(m.GitTotalCommits / 50.0, 1.0);
        var collaborationDensity = Math.Min(m.GitContributors / 10.0, 1.0);

        var processFriction = (churn * 0.7) + (collaborationDensity * 0.3);

        // 3. Cognitive Friction (Lizard/Metrics) - Is it a "Brain Drain"?
        // High complexity per line makes it harder to reason about.
        var density = m.LinesOfCode > 0 ? (double)m.CyclomaticComplexity / m.LinesOfCode : 0;
        var cognitiveFriction = Math.Min(m.CyclomaticComplexity / 20.0, 1.0);

        // 4. Final Weighted Friction
        // 40% Architecture, 30% Team/History, 30% Code Quality
        var friction =
            (structuralFriction * 0.4) + (processFriction * 0.3) + (cognitiveFriction * 0.3);

        return Clamp(friction, 0, 1);
    }

    private double CalculateAutonomy(NodeMetrics m)
    {
        var totalDegree = Math.Max(m.TotalDegree, 1);
        var dependencyRatio = m.OutgoingEdges / (double)totalDegree;

        // 2. The "Blast Radius" Penalty
        // If a file has 30 outgoing edges, even if the ratio is okay,
        // it's still "Fragile" because any of those 30 files could break it.
        var blastRadius = Math.Min(m.OutgoingEdges / 30.0, 1.0);

        // Weighted Autonomy: 80% Ratio, 20% Absolute Count
        var autonomy = (1 - dependencyRatio) * 0.8 + (1 - blastRadius) * 0.2;
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
