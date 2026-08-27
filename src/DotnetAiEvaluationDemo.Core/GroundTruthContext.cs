using Microsoft.Extensions.AI.Evaluation;

namespace DotnetAiEvaluationDemo.Core;

/// <summary>Supplies the reference answer expected by reference-based evaluators.</summary>
public sealed class GroundTruthContext(string value) : EvaluationContext("GroundTruth", value)
{
    public string Value { get; } = value;
}

/// <summary>Supplies retrieved context to context-aware evaluators.</summary>
public sealed class RetrievedContext(string value) : EvaluationContext("Context", value)
{
    public string Value { get; } = value;
}
