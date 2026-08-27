namespace DotnetAiEvaluationDemo.Core;

public sealed record EvaluationReport(
    string Evaluator,
    IReadOnlyList<EvaluationMetricResult> Metrics,
    bool Passed);

public sealed record EvaluationMetricResult(
    string Name,
    double? NumericValue,
    string? Rating,
    bool Passed,
    string? Reason);
