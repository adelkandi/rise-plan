using backend.Domain;

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
