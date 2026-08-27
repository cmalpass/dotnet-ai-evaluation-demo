using System.Text.Json.Serialization;

namespace DotnetAiEvaluationDemo.Core;

/// <summary>A single production-shaped record from the offline evaluation set.</summary>
public sealed class EvaluationCase
{
    public string Id { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public string Response { get; init; } = string.Empty;
    public string ReferenceAnswer { get; init; } = string.Empty;
    public IReadOnlyList<string> Checks { get; init; } = [];
    public AnswerContract AnswerContract { get; init; } = new();
    public string? RetrievedContext { get; init; }
    public IReadOnlyList<string> ExpectedContextLabels { get; init; } = [];
    public IReadOnlyList<string> RetrievedContextLabels { get; init; } = [];
    public IReadOnlyList<ToolCallRecord> ExpectedToolCalls { get; init; } = [];
    public IReadOnlyList<ToolCallRecord> ActualToolCalls { get; init; } = [];
    public SafetyContract Safety { get; init; } = new();

    public bool HasCheck(string name) => Checks.Contains(name, StringComparer.OrdinalIgnoreCase);
}

public sealed class AnswerContract
{
    public bool RequireExact { get; init; }
    public double MinimumOverlap { get; init; } = 0.75;
}

public sealed record ToolCallRecord(
    string Name,
    IReadOnlyDictionary<string, string> Arguments);

public sealed class SafetyContract
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SafetyDisposition ExpectedDisposition { get; init; } = SafetyDisposition.Allowed;

    public IReadOnlyList<string> RequiredResponseMarkers { get; init; } = [];
    public IReadOnlyList<string> ForbiddenResponseMarkers { get; init; } = [];
}

public enum SafetyDisposition
{
    Allowed,
    Refuse
}
