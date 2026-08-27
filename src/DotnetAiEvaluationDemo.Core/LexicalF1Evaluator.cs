using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace DotnetAiEvaluationDemo.Core;

/// <summary>A deterministic, provider-free F1 evaluator for local development and regression tests.</summary>
public sealed partial class LexicalF1Evaluator : IEvaluator
{
    public const string F1MetricName = "F1";

    public IReadOnlyCollection<string> EvaluationMetricNames => [F1MetricName];

    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modelResponse);
        cancellationToken.ThrowIfCancellationRequested();

        var reference = additionalContext?.OfType<GroundTruthContext>().SingleOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("A GroundTruth evaluation context is required.", nameof(additionalContext));
        }

        var score = CalculateF1(Tokenize(modelResponse.Text), Tokenize(reference));
        var passed = score >= 0.5;
        var metric = new NumericMetric(F1MetricName, score, "Lexical F1 uses shared token counts; the default pass cutoff is 0.5.")
        {
            Interpretation = new EvaluationMetricInterpretation(
                passed ? EvaluationRating.Good : EvaluationRating.Unacceptable,
                failed: !passed,
                reason: passed
                    ? "The response meets the default lexical F1 cutoff."
                    : "The response is below the default lexical F1 cutoff.")
        };

        return ValueTask.FromResult(new EvaluationResult(metric));
    }

    private static double CalculateF1(IReadOnlyList<string> response, IReadOnlyList<string> reference)
    {
        if (response.Count == 0 || reference.Count == 0)
        {
            return 0;
        }

        var referenceCounts = reference.GroupBy(token => token).ToDictionary(group => group.Key, group => group.Count());
        var overlap = response
            .GroupBy(token => token)
            .Sum(group => Math.Min(group.Count(), referenceCounts.GetValueOrDefault(group.Key)));
        var precision = (double)overlap / response.Count;
        var recall = (double)overlap / reference.Count;

        return precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
    }

    private static IReadOnlyList<string> Tokenize(string? value) =>
        TokenRegex().Matches(value ?? string.Empty).Select(match => match.Value.ToUpperInvariant()).ToArray();

    [GeneratedRegex("[\\p{L}\\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
