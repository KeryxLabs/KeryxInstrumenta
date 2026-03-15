using System;
using AdaptiveCodecContextEngine.Models;
using AdaptiveCodecContextEngine.Models.Lsp;
using Xunit;

namespace AdaptiveCodecContextEngineTests
{
    public class AvecCalculatorTests(ITestOutputHelper helper)
    {
        [Fact]
        public void Calculate_ReturnsExpectedScores_ForSampleNodeMetrics()
        {
            // Arrange: set up the sample NodeMetrics from the user example
            var metrics = new NodeMetrics
            {
                LinesOfCode = 45,
                CyclomaticComplexity = 34,
                Parameters = 1,
                IncomingEdges = 0,
                OutgoingEdges = 0,
                TotalDegree = 0, // Will be clamped to 1 in code
                GitTotalCommits = 2,
                GitContributors = 1,
                GitAvgDaysBetweenChanges = 0.13945601851851852,
                TestLineCoverage = 0,
                TestBranchCoverage = 0
            };
            var weights = new AvecWeights();
            var calculator = new AvecCalculator(weights);

            // Act
            var scores = calculator.Calculate(metrics);

            // Assert: print or check the output for now
            helper.WriteLine($"Stability: {scores.Stability}");
            helper.WriteLine($"Logic: {scores.Logic}");
            helper.WriteLine($"Friction: {scores.Friction}");
            helper.WriteLine($"Autonomy: {scores.Autonomy}");
            // For now, just assert that the method runs and returns values in [0,1]
            Assert.InRange(scores.Stability, 0, 1);
            Assert.InRange(scores.Logic, 0, 1);
            Assert.InRange(scores.Friction, 0, 1);
            Assert.InRange(scores.Autonomy, 0, 1);
        }
    }
}
