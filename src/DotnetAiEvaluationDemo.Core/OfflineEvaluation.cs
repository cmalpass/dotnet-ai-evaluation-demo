using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotnetAiEvaluationDemo.Core;

public interface IDatasetEvaluator
{
    string Name { get; }

    bool AppliesTo(EvaluationCase evaluationCase);

    IReadOnlyList<DatasetMetric> Evaluate(EvaluationCase evaluationCase);
}

public sealed record DatasetMetric(
    string Name,
    double Score,
    bool Passed,
    string Reason);

public sealed record DatasetCaseResult(
    string CaseId,
    string Category,
    IReadOnlyList<DatasetMetric> Metrics)
{
    public bool Passed => Metrics.Count > 0 && Metrics.All(metric => metric.Passed);
}

public sealed record DatasetMetricSummary(
    string Name,
    int Cases,
    int PassedCases,
    double AverageScore)
{
    public double PassRate => Cases == 0 ? 0 : (double)PassedCases / Cases;
}

public sealed record DatasetEvaluationRun(
    IReadOnlyList<DatasetCaseResult> Cases,
    IReadOnlyList<DatasetMetricSummary> Metrics)
{
    public int TotalCases => Cases.Count;
    public int PassedCases => Cases.Count(evaluationCase => evaluationCase.Passed);
    public double PassRate => TotalCases == 0 ? 0 : (double)PassedCases / TotalCases;
    public bool Passed => TotalCases > 0 && Cases.All(evaluationCase => evaluationCase.Passed);
}

public sealed class OfflineDatasetRunner(IEnumerable<IDatasetEvaluator> evaluators)
{
    private readonly IReadOnlyList<IDatasetEvaluator> evaluators = evaluators?.ToArray()
        ?? throw new ArgumentNullException(nameof(evaluators));

    public DatasetEvaluationRun Run(IEnumerable<EvaluationCase> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);

        var results = cases
            .Select(evaluationCase => new DatasetCaseResult(
                evaluationCase.Id,
                evaluationCase.Category,
                evaluators
                    .Where(evaluator => evaluator.AppliesTo(evaluationCase))
                    .SelectMany(evaluator => evaluator.Evaluate(evaluationCase))
                    .ToArray()))
            .ToArray();

        var metrics = results
            .SelectMany(result => result.Metrics)
            .GroupBy(metric => metric.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DatasetMetricSummary(
                group.Key,
                group.Count(),
                group.Count(metric => metric.Passed),
                group.Average(metric => metric.Score)))
            .OrderBy(summary => summary.Name, StringComparer.Ordinal)
            .ToArray();

        return new DatasetEvaluationRun(results, metrics);
    }
}

public static class EvaluationDatasetLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    static EvaluationDatasetLoader()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static IReadOnlyList<EvaluationCase> Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return LoadLines(File.ReadLines(path));
    }

    public static IReadOnlyList<EvaluationCase> LoadLines(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var cases = new List<EvaluationCase>();
        var lineNumber = 0;

        foreach (var line in lines)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var evaluationCase = JsonSerializer.Deserialize<EvaluationCase>(line, SerializerOptions)
                    ?? throw new JsonException("The JSON value was null.");
                Validate(evaluationCase, lineNumber);
                cases.Add(evaluationCase);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Invalid evaluation JSONL at line {lineNumber}.", exception);
            }
        }

        return cases;
    }

    private static void Validate(EvaluationCase evaluationCase, int lineNumber)
    {
        if (string.IsNullOrWhiteSpace(evaluationCase.Id))
        {
            throw new InvalidDataException($"Evaluation case at line {lineNumber} has no id.");
        }

        if (string.IsNullOrWhiteSpace(evaluationCase.Prompt) ||
            string.IsNullOrWhiteSpace(evaluationCase.Response) ||
            string.IsNullOrWhiteSpace(evaluationCase.ReferenceAnswer))
        {
            throw new InvalidDataException($"Evaluation case '{evaluationCase.Id}' must include prompt, response, and referenceAnswer.");
        }

        if (evaluationCase.Checks.Count == 0)
        {
            throw new InvalidDataException($"Evaluation case '{evaluationCase.Id}' must select at least one check.");
        }

        if (evaluationCase.AnswerContract.MinimumOverlap is < 0 or > 1)
        {
            throw new InvalidDataException($"Evaluation case '{evaluationCase.Id}' has an overlap threshold outside 0..1.");
        }
    }
}

public sealed class EvaluationProfile
{
    public string Dataset { get; init; } = string.Empty;
    public string DatasetVersion { get; init; } = string.Empty;
    public ProfileApplication Application { get; init; } = new();
    public ProfileEvaluators Evaluators { get; init; } = new();
    public ProfileExecution Execution { get; init; } = new();
}

public sealed class ProfileApplication
{
    public string Mode { get; init; } = string.Empty;
    public int RetrievalTopK { get; init; }
    public string ToolSideEffects { get; init; } = string.Empty;
}

public sealed class ProfileEvaluators
{
    public IReadOnlyList<string> Deterministic { get; init; } = [];
    public IReadOnlyList<string> LiveQuality { get; init; } = [];
    public string Safety { get; init; } = string.Empty;
}

public sealed class ProfileExecution
{
    public int SamplesPerCase { get; init; }
    public string Cache { get; init; } = string.Empty;
    public string Report { get; init; } = string.Empty;
}

public static class EvaluationProfileLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static EvaluationProfile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var profile = JsonSerializer.Deserialize<EvaluationProfile>(File.ReadAllText(path), SerializerOptions)
            ?? throw new InvalidDataException("The evaluation profile was empty.");

        if (string.IsNullOrWhiteSpace(profile.Dataset) || string.IsNullOrWhiteSpace(profile.DatasetVersion))
        {
            throw new InvalidDataException("The evaluation profile must include dataset and datasetVersion.");
        }

        return profile;
    }
}
