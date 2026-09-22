using backend.Domain;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace backend.Planning;

public interface IPlanningAgent
{
    Task<PlanningResponse> ProcessMessageAsync(
        string userId,
        Guid? conversationId,
        string message,
        CancellationToken cancellationToken);
}

public sealed class DevelopmentPlanningAgent(IPlanningContextReader contextReader) : IPlanningAgent
{
    public async Task<PlanningResponse> ProcessMessageAsync(
        string userId,
        Guid? conversationId,
        string message,
        CancellationToken cancellationToken)
    {
        var context = await contextReader.ReadAsync(userId, conversationId, cancellationToken);
        var suggestions = BuildSuggestions(message, context);
        var content = suggestions.Count == 0
            ? "I hear you. Tell me what is competing for your attention, and I will help separate what is fixed, flexible, and uncertain."
            : suggestions[0].Description ?? suggestions[0].Title;

        return new("text", content, suggestions, context);
    }

    public sealed class OpenAiPlanningAgent(
        IPlanningContextReader contextReader,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration) : IPlanningAgent
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public async Task<PlanningResponse> ProcessMessageAsync(
            string userId,
            Guid? conversationId,
            string message,
            CancellationToken cancellationToken)
        {
            var context = await contextReader.ReadAsync(userId, conversationId, cancellationToken);
            var apiKey = configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured.");
            var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";
            var client = httpClientFactory.CreateClient("OpenAI");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var request = new
            {
                model,
                temperature = 0.2,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new { role = "system", content = BuildSystemPrompt(context) },
                    new { role = "user", content = message }
                }
            };

            using var response = await client.PostAsJsonAsync("v1/chat/completions", request, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<OpenAiResponse>(JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("OpenAI returned an empty response.");
            var content = payload.Choices.FirstOrDefault()?.Message.Content
                ?? throw new InvalidOperationException("OpenAI returned no assistant content.");
            var generated = JsonSerializer.Deserialize<GeneratedPlanningResponse>(content, JsonOptions)
                ?? throw new InvalidOperationException("OpenAI returned an invalid planning response.");

            return new(
                generated.Type ?? "text",
                generated.Content ?? string.Empty,
                generated.Suggestions ?? [],
                context);
        }

        private static string BuildSystemPrompt(PlanningContextSnapshot context) =>
            $$"""
            You are Rise, a calm planning companion. Understand the user's situation before organizing it.
            Do not turn possibilities or tentative events into tasks automatically. Ask a useful question when
            missing information materially affects a decision. Return only valid JSON with this shape:
            { "type": "text|question|suggestion|decision|summary", "content": "string",
              "suggestions": [{ "type": "string", "title": "string", "description": "string" }] }

            Current user context:
            Recent messages: {{JsonSerializer.Serialize(context.RecentMessages)}}
            Open tasks: {{JsonSerializer.Serialize(context.OpenTasks)}}
            Upcoming commitments: {{JsonSerializer.Serialize(context.Commitments)}}
            Today's plan: {{JsonSerializer.Serialize(context.TodayPlan)}}
            """;

        private sealed record OpenAiResponse(IReadOnlyList<OpenAiChoice> Choices);
        private sealed record OpenAiChoice(OpenAiMessage Message);
        private sealed record OpenAiMessage(string Content);
        private sealed record GeneratedPlanningResponse(
            string? Type,
            string? Content,
            IReadOnlyList<PlanningSuggestion>? Suggestions);
    }

    private static List<PlanningSuggestion> BuildSuggestions(string message, PlanningContextSnapshot context)
    {
        var suggestions = new List<PlanningSuggestion>();
        if (context.OpenTasks.Count > 0)
        {
            var firstTask = context.OpenTasks[0];
            suggestions.Add(new(
                "suggestion",
                "Start with one clear task",
                $"You have {context.OpenTasks.Count} open task(s). {firstTask.Title} is a useful place to begin."));
        }

        if (message.Contains("lost", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("overwhelmed", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add(new(
                "question",
                "What is fixed today?",
                "Which commitment cannot move? We can protect that first and keep the rest flexible."));
        }

        if (message.Contains("maybe", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("don't know", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("uncertain", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add(new(
                "decision",
                "Keep uncertain items undecided",
                "I will not turn a possibility or tentative event into a task until you decide what you want."));
        }

        if (context.Commitments.Count > 0)
        {
            suggestions.Add(new(
                "summary",
                "Protect your commitments",
                $"You have {context.Commitments.Count} upcoming commitment(s) in your context."));
        }

        return suggestions;
    }
}
