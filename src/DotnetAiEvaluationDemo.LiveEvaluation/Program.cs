using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using OpenAI;

var endpoint = Environment.GetEnvironmentVariable("EVAL_MODEL_ENDPOINT")
    ?? throw new InvalidOperationException(
        "Set EVAL_MODEL_ENDPOINT to the OpenAI-compatible /v1 endpoint to test.");
var model = Environment.GetEnvironmentVariable("EVAL_MODEL_ID")
    ?? throw new InvalidOperationException(
        "Set EVAL_MODEL_ID to a model returned by the endpoint's /v1/models resource.");
var apiKey = Environment.GetEnvironmentVariable("EVAL_MODEL_API_KEY") ?? "local";

if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
{
    throw new InvalidOperationException("EVAL_MODEL_ENDPOINT must be an absolute URI.");
}

var options = new OpenAIClientOptions { Endpoint = endpointUri };

#pragma warning disable OPENAI001
var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), options)
    .GetChatClient(model)
    .AsIChatClient();
#pragma warning restore OPENAI001

var messages = new List<ChatMessage>
{
    new(ChatRole.User, "Explain why representative evaluation datasets matter in one concise paragraph.")
};

var response = await chatClient.GetResponseAsync(
    messages,
    new ChatOptions
    {
        Temperature = 0,
        MaxOutputTokens = 512,
        Reasoning = new ReasoningOptions
        {
            Effort = ReasoningEffort.None,
            Output = ReasoningOutput.None
        }
    });

// The evaluation package supplies its own ChatOptions to the judge client. This
// wrapper applies local-model-safe defaults to those requests as well.
var judgeClient = new EvaluationChatClient(chatClient);
var evaluator = new CompositeEvaluator(
    new RelevanceEvaluator(),
    new GroundednessEvaluator());
var candidateResponse = response.Text.Length > 0
    ? response
    : new ChatResponse(new ChatMessage(
        ChatRole.Assistant,
        "Representative datasets capture the inputs and expected behaviors that matter in production, so an evaluation suite can detect regressions instead of relying on anecdotal prompts."));
var groundingContext = new GroundednessEvaluatorContext(
    "Representative evaluation datasets capture production inputs and expected behaviors. " +
    "They expose regressions, measure generalization, and make quality changes comparable over time.");
var evaluation = await evaluator.EvaluateAsync(
    messages,
    candidateResponse,
    new ChatConfiguration(judgeClient),
    additionalContext: [groundingContext]);
var relevance = evaluation.Get<NumericMetric>(RelevanceEvaluator.RelevanceMetricName);
var groundedness = evaluation.Get<NumericMetric>(GroundednessEvaluator.GroundednessMetricName);

Console.WriteLine($"Endpoint: {endpoint}");
Console.WriteLine($"Model: {Path.GetFileName(model)}");
Console.WriteLine($"Generated response length: {response.Text.Length} characters");
Console.WriteLine($"Contains <think> markup: {response.Text.Contains("<think>", StringComparison.OrdinalIgnoreCase)}");
Console.WriteLine($"Evaluation response source: {(response.Text.Length > 0 ? "generated" : "deterministic fallback")}");
WriteMetric("Relevance", relevance);
WriteMetric("Groundedness", groundedness);

if (!IsUsable(relevance) || !IsUsable(groundedness))
{
    Console.WriteLine("Evaluation did not produce usable metrics; inspect the model's structured judge response and reasoning settings.");
    Environment.ExitCode = 2;
}

static void WriteMetric(string name, NumericMetric metric)
{
    var score = metric.Value is null || double.IsNaN(metric.Value.Value)
        ? "unavailable"
        : $"{metric.Value.Value}/5";

    Console.WriteLine($"{name}: {score}");
    Console.WriteLine($"{name} rating: {metric.Interpretation?.Rating}");
    Console.WriteLine($"{name} passed: {metric.Interpretation?.Failed == false}");
    Console.WriteLine($"{name} reason: {metric.Reason ?? metric.Interpretation?.Reason ?? "(none returned)"}");
}

static bool IsUsable(NumericMetric metric) =>
    metric.Interpretation is not null &&
    metric.Value.HasValue &&
    !double.IsNaN(metric.Value.Value) &&
    !double.IsInfinity(metric.Value.Value) &&
    !metric.ContainsDiagnostics(diagnostic => diagnostic.Severity >= EvaluationDiagnosticSeverity.Warning);

sealed class EvaluationChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var judgeOptions = options is null ? new ChatOptions() : options.Clone();
        judgeOptions.MaxOutputTokens = Math.Max(judgeOptions.MaxOutputTokens ?? 0, 4096);
        judgeOptions.Temperature = 0;
        judgeOptions.Reasoning = new ReasoningOptions
        {
            Effort = ReasoningEffort.None,
            Output = ReasoningOutput.None
        };

        return base.GetResponseAsync(messages, judgeOptions, cancellationToken);
    }
}
