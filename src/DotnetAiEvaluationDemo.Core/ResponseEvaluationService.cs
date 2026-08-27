using Microsoft.Extensions.AI.Evaluation;

namespace DotnetAiEvaluationDemo.Core;

public sealed class ResponseEvaluationService(IEvaluator evaluator)
{
    public async ValueTask<EvaluationReport> EvaluateAsync(
        EvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);

        var contexts = new List<EvaluationContext>
        {
            new GroundTruthContext(request.ReferenceAnswer)
        };

        if (!string.IsNullOrWhiteSpace(request.Context))
        {
            contexts.Add(new RetrievedContext(request.Context));
        }

        var result = await evaluator.EvaluateAsync(
            request.Prompt,
            request.Response,
            additionalContext: contexts,
            cancellationToken: cancellationToken);

        var metrics = result.Metrics.Values.Select(ToMetricResult).ToArray();

        return new EvaluationReport(
            evaluator.GetType().Name,
            metrics,
            metrics.Length > 0 && metrics.All(metric => metric.Passed));
    }

    private static EvaluationMetricResult ToMetricResult(EvaluationMetric metric)
    {
        var numericValue = metric is NumericMetric numericMetric
            ? numericMetric.Value
            : null;
        var interpretation = metric.Interpretation;

        return new EvaluationMetricResult(
            metric.Name,
            numericValue,
            interpretation?.Rating.ToString(),
            interpretation is { Failed: false },
            metric.Reason ?? interpretation?.Reason);
    }

    private static void Validate(EvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequired(request.Prompt, nameof(request.Prompt));
        ValidateRequired(request.Response, nameof(request.Response));
        ValidateRequired(request.ReferenceAnswer, nameof(request.ReferenceAnswer));

        ValidateLength(request.Prompt, nameof(request.Prompt), 4_000);
        ValidateLength(request.Response, nameof(request.Response), 12_000);
        ValidateLength(request.ReferenceAnswer, nameof(request.ReferenceAnswer), 12_000);
        ValidateLength(request.Context, nameof(request.Context), 20_000);
    }

    private static void ValidateRequired(string? value, string name) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);

    private static void ValidateLength(string? value, string name, int maximumLength)
    {
        if (value?.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{name} must be {maximumLength:N0} characters or fewer.",
                name);
        }
    }
}
