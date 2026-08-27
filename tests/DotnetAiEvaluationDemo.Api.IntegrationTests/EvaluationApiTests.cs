using System.Net;
using System.Net.Http.Json;
using DotnetAiEvaluationDemo.Core;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DotnetAiEvaluationDemo.Api.IntegrationTests;

public sealed class EvaluationApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public EvaluationApiTests(WebApplicationFactory<Program> factory) => client = factory.CreateClient();

    [Fact]
    public async Task Root_endpoint_describes_the_safe_offline_mode()
    {
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StatusResponse>();
        Assert.NotNull(body);
        Assert.Equal("offline", body.Mode);
        Assert.Equal("LexicalF1Evaluator", body.Evaluator);
    }

    [Fact]
    public async Task Evaluation_endpoint_returns_a_passing_report()
    {
        var response = await client.PostAsJsonAsync(
            "/api/evaluations",
            new EvaluationRequest(
                "What is the retention period?",
                "The retention period is 30 days.",
                "The retention period is 30 days.",
                "Policy: records are retained for 30 days."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<EvaluationReport>();
        Assert.NotNull(report);
        Assert.True(report.Passed);
        Assert.Equal(1, Assert.Single(report.Metrics).NumericValue);
    }

    [Fact]
    public async Task Evaluation_endpoint_returns_validation_problem_for_missing_input()
    {
        var response = await client.PostAsJsonAsync(
            "/api/evaluations",
            new { Prompt = "What?", Response = "An answer.", ReferenceAnswer = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ReferenceAnswer", body, StringComparison.Ordinal);
    }

    private sealed record StatusResponse(string Service, string Evaluator, string Mode, string Endpoint);
}
