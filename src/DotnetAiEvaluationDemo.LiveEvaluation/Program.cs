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
var evaluator = new RelevanceEvaluator();
var candidateResponse = response.Text.Length > 0
    ? response
    : new ChatResponse(new ChatMessage(
        ChatRole.Assistant,
        "Representative datasets capture the inputs and expected behaviors that matter in production, so an evaluation suite can detect regressions instead of relying on anecdotal prompts."));
var evaluation = await evaluator.EvaluateAsync(
    messages,
    candidateResponse,
    new ChatConfiguration(judgeClient));
var relevance = evaluation.Get<NumericMetric>(RelevanceEvaluator.RelevanceMetricName);
var relevanceValue = relevance.Value;

Console.WriteLine($"Endpoint: {endpoint}");
Console.WriteLine($"Model: {Path.GetFileName(model)}");
Console.WriteLine($"Generated response length: {response.Text.Length} characters");
Console.WriteLine($"Contains <think> markup: {response.Text.Contains("<think>", StringComparison.OrdinalIgnoreCase)}");
Console.WriteLine($"Evaluation response source: {(response.Text.Length > 0 ? "generated" : "deterministic fallback")}");
Console.WriteLine($"Relevance: {(!relevanceValue.HasValue || double.IsNaN(relevanceValue.Value) ? "unavailable" : $"{relevanceValue.Value}/5")}");
Console.WriteLine($"Rating: {relevance.Interpretation?.Rating}");
Console.WriteLine($"Passed: {relevance.Interpretation?.Failed == false}");
Console.WriteLine($"Reason: {relevance.Reason ?? relevance.Interpretation?.Reason ?? "(none returned)"}");

if (relevance.Interpretation is null || !relevanceValue.HasValue || double.IsNaN(relevanceValue.Value))
{
    Console.WriteLine("Evaluation did not produce a usable metric; inspect the model's structured judge response and reasoning settings.");
    Environment.ExitCode = 2;
}

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
