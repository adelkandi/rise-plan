using backend.Domain;

namespace backend.Planning;

public sealed record ChatMessageRequest(Guid? ConversationId, string Message);
public sealed record ChatMessageResponse(Guid ConversationId, PlanningResponse Planning);

public sealed record PlanningResponse(
    string Type,
    string Content,
    IReadOnlyList<PlanningSuggestion> Suggestions,
    PlanningContextSnapshot Context);

public sealed record PlanningSuggestion(string Type, string Title, string? Description);

public sealed record PlanningContextSnapshot(
    IReadOnlyList<string> RecentMessages,
    IReadOnlyList<TaskSummary> OpenTasks,
    IReadOnlyList<CommitmentSummary> Commitments,
    DailyPlanSummary? TodayPlan);

public sealed record TaskSummary(Guid Id, string Title, Priority Priority, DateTimeOffset? DueDate);
public sealed record CommitmentSummary(Guid Id, string Title, DateTimeOffset StartTime, DateTimeOffset EndTime, bool IsFlexible);
public sealed record DailyPlanSummary(Guid Id, DateOnly Date, string? Summary, int ItemCount);
