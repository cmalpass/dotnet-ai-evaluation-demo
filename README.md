# AI response evaluation in .NET 10

This self-contained sample demonstrates an evaluation boundary around an AI response. The API accepts a prompt, generated response, reference answer, and optional retrieved context. The core service invokes a custom deterministic `IEvaluator` built on the stable `Microsoft.Extensions.AI.Evaluation` abstractions and maps its result to a small, stable HTTP contract.

The default path is deliberately safe and deterministic:

- No API key, model download, cloud account, or network call is required at runtime.
- `LexicalF1Evaluator` compares the response with a reference answer using lexical precision and recall.
- Unit tests exercise the evaluation service and integration tests exercise the running HTTP pipeline through `WebApplicationFactory<Program>`.
- Optional context is carried through the evaluation context model so the same service can host groundedness or other context-aware evaluators.

The evaluator is injected as `IEvaluator`. That is the seam for opting into model-graded evaluators such as `RelevanceEvaluator` from `Microsoft.Extensions.AI.Evaluation.Quality`. A real provider should be registered only behind explicit configuration and server-side secret management; it must never be enabled implicitly by a first run or from client-supplied input.

## Prerequisites

- .NET 10 SDK

The project was authored and verified with the .NET 10 SDK. Package versions are pinned in the project files so restore is repeatable.

## Run from a clean checkout

From this directory:

```bash
dotnet restore DotnetAiEvaluationDemo.sln
dotnet run --project src/DotnetAiEvaluationDemo.Api/DotnetAiEvaluationDemo.Api.csproj
```

Open the URL printed by ASP.NET Core. The root endpoint returns the active `offline` mode and evaluator.

Evaluate a response from a second terminal, replacing the port if necessary:

```bash
curl -i http://localhost:5000/api/evaluations \
  -H 'Content-Type: application/json' \
  --data '{
    "prompt": "What is the retention period?",
    "response": "The retention period is 30 days.",
    "referenceAnswer": "The retention period is 30 days.",
    "context": "Policy: records are retained for 30 days."
  }'
```

An exact reference answer produces an F1 score of `1` and a passing report.

## Test

```bash
dotnet test DotnetAiEvaluationDemo.sln
```

The test suite is entirely offline. It contains core unit tests and HTTP integration tests; no component/UI test applies because this sample intentionally exposes an API rather than a frontend.

For the same checks used by CI:

```bash
dotnet build DotnetAiEvaluationDemo.sln --configuration Release
dotnet test DotnetAiEvaluationDemo.sln --configuration Release --no-build
```

## Opting into an LLM-graded evaluator

The sample keeps the provider out of the default dependency graph. To add a model-graded path, install the current stable `Microsoft.Extensions.AI.Evaluation.Quality` package and a provider adapter that implements `IChatClient`, then replace the `F1Evaluator` registration with a configured `RelevanceEvaluator` and pass a `ChatConfiguration` to the evaluation service. Keep the provider endpoint and credentials in server-side configuration such as user secrets, environment variables, or managed identity. Do not accept them in `EvaluationRequest`.

This separation is intentional: deterministic metrics are appropriate for every pull request, while model-graded evaluations should be explicitly enabled, cached, and thresholded against a versioned dataset because they consume model capacity and can vary between runs.

## Repository layout

```text
src/
  DotnetAiEvaluationDemo.Core/                 Evaluation contract and service
  DotnetAiEvaluationDemo.Api/                  Minimal API and DI registration
tests/
  DotnetAiEvaluationDemo.Core.Tests/           Unit tests
  DotnetAiEvaluationDemo.Api.IntegrationTests/ WebApplicationFactory tests
```

## Blog post

The companion post will be published at [AI Agent Evaluation in .NET — Measuring What Matters](https://chrismalpass.com/posts/ai-agent-evaluation-dotnet/).

## License

This sample is available under the [MIT License](LICENSE).
