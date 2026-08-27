using DotnetAiEvaluationDemo.Core;
using Xunit;

namespace DotnetAiEvaluationDemo.Core.Tests;

public sealed class ResponseEvaluationServiceTests
{
    private readonly ResponseEvaluationService service = new(new LexicalF1Evaluator());

    [Fact]
    public async Task Exact_reference_answer_passes_with_a_perfect_f1_score()
    {
        var request = new EvaluationRequest(
            "What does the service do?",
            "It evaluates AI responses with deterministic metrics.",
            "It evaluates AI responses with deterministic metrics.");

        var report = await service.EvaluateAsync(request);

        var metric = Assert.Single(report.Metrics);
        Assert.Equal("F1", metric.Name);
        Assert.Equal(1, metric.NumericValue);
        Assert.True(metric.Passed);
        Assert.True(report.Passed);
    }

    [Fact]
    public async Task Unrelated_answer_fails_the_default_f1_cutoff()
    {
        var request = new EvaluationRequest(
            "What does the service do?",
            "The weather is sunny today.",
            "It evaluates AI responses with deterministic metrics.");

        var report = await service.EvaluateAsync(request);

        var metric = Assert.Single(report.Metrics);
        Assert.Equal(0, metric.NumericValue);
        Assert.False(metric.Passed);
        Assert.False(report.Passed);
    }

    [Fact]
    public async Task Missing_reference_answer_is_rejected_before_evaluator_runs()
    {
        var request = new EvaluationRequest("Prompt", "Response", " ");

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.EvaluateAsync(request).AsTask());

        Assert.Equal("ReferenceAnswer", exception.ParamName);
    }

    [Fact]
    public async Task Retrieved_context_is_accepted_without_changing_the_offline_metric()
    {
        var request = new EvaluationRequest(
            "What is the retention period?",
            "The retention period is 30 days.",
            "The retention period is 30 days.",
            "Policy: records are retained for 30 days.");

        var report = await service.EvaluateAsync(request);

        Assert.True(report.Passed);
        Assert.Equal(1, Assert.Single(report.Metrics).NumericValue);
    }
}
