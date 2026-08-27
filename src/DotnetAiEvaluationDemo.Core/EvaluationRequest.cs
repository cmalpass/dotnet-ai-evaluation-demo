namespace DotnetAiEvaluationDemo.Core;

public sealed record EvaluationRequest(
    string Prompt,
    string Response,
    string ReferenceAnswer,
    string? Context = null);
