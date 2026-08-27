using DotnetAiEvaluationDemo.Core;
using Microsoft.Extensions.AI.Evaluation;

var builder = WebApplication.CreateBuilder(args);

// LexicalF1Evaluator is deterministic and does not call an LLM. A production application can
// replace this registration with a model-graded IEvaluator behind explicit configuration.
builder.Services.AddSingleton<IEvaluator, LexicalF1Evaluator>();
builder.Services.AddSingleton<ResponseEvaluationService>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "dotnet-ai-evaluation-demo",
    evaluator = "LexicalF1Evaluator",
    mode = "offline",
    endpoint = "POST /api/evaluations"
}));

app.MapPost("/api/evaluations", async (
    EvaluationRequest request,
    ResponseEvaluationService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        var report = await service.EvaluateAsync(request, cancellationToken);
        return Results.Ok(report);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [exception.ParamName ?? "request"] = [exception.Message]
        });
    }
});

app.Run();

public partial class Program;
