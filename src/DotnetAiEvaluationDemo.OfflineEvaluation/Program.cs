using System.Text.Json;
using DotnetAiEvaluationDemo.Core;

var datasetPath = GetOption(args, "--dataset") ?? Path.Combine(AppContext.BaseDirectory, "data", "evaluation-cases.jsonl");
var profilePath = GetOption(args, "--profile") ?? Path.Combine(AppContext.BaseDirectory, "data", "evaluation-profile.json");
var failOnRegression = args.Contains("--fail-on-regression", StringComparer.OrdinalIgnoreCase);
var profile = EvaluationProfileLoader.Load(profilePath);
var cases = EvaluationDatasetLoader.Load(datasetPath);
var runner = new OfflineDatasetRunner(
[
    new AnswerQualityEvaluator(),
    new RetrievalLabelEvaluator(),
    new ToolTrajectoryEvaluator(),
    new SafetyPolicyEvaluator()
]);
var run = runner.Run(cases);

Console.WriteLine($"Dataset: {datasetPath}");
Console.WriteLine($"Profile: {profile.Dataset} v{profile.DatasetVersion} ({profile.Application.Mode})");
Console.WriteLine($"Cases: {run.PassedCases}/{run.TotalCases} passed ({run.PassRate:P1})");
Console.WriteLine();
Console.WriteLine("Metric                         Cases  Passed  Pass rate  Average");
Console.WriteLine("-----------------------------------------------------------------");

foreach (var metric in run.Metrics)
{
    Console.WriteLine($"{metric.Name,-30} {metric.Cases,5} {metric.PassedCases,8} {metric.PassRate,9:P1} {metric.AverageScore,8:F2}");
}

Console.WriteLine();
foreach (var evaluationCase in run.Cases)
{
    Console.WriteLine($"[{(evaluationCase.Passed ? "PASS" : "FAIL")}] {evaluationCase.CaseId} ({evaluationCase.Category})");
    foreach (var metric in evaluationCase.Metrics)
    {
        Console.WriteLine($"      {metric.Name}: {metric.Score:F2} {(metric.Passed ? "pass" : "fail")} — {metric.Reason}");
    }
}

Console.WriteLine();
Console.WriteLine(JsonSerializer.Serialize(new
{
    run.TotalCases,
    run.PassedCases,
    run.PassRate,
    Metrics = run.Metrics,
    Cases = run.Cases
}, new JsonSerializerOptions { WriteIndented = true }));

if (failOnRegression && !run.Passed)
{
    Environment.ExitCode = 1;
}

static string? GetOption(string[] arguments, string option)
{
    var index = Array.FindIndex(arguments, argument => string.Equals(argument, option, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}
