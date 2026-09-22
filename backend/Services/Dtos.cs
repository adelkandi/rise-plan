using backend.Domain;

namespace backend.Services;

public sealed record ConversationResponse(Guid Id, string? Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record MessageResponse(Guid Id, MessageRole Role, string Content, DateTimeOffset CreatedAt);
public sealed record ConversationDetailsResponse(ConversationResponse Conversation, IReadOnlyList<MessageResponse> Messages);
public sealed record CreateConversationRequest(string? Title);
public sealed record CreateMessageRequest(string Content);

public sealed record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    backend.Domain.TaskStatus Status,
    Priority Priority,
    int? EstimatedMinutes,
    DateTimeOffset? DueDate,
    DateTimeOffset? CompletedAt);

public sealed record CreateTaskRequest(
    string Title,
    string? Description,
    Priority Priority = Priority.Normal,
    int? EstimatedMinutes = null,
    DateTimeOffset? DueDate = null);

public sealed record UpdateTaskRequest(
    string? Title,
    string? Description,
    backend.Domain.TaskStatus? Status,
    Priority? Priority,
    int? EstimatedMinutes,
    DateTimeOffset? DueDate);

public sealed record CommitmentResponse(
    Guid Id,
    string Title,
    string? Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string? Location,
    bool IsFlexible);

public sealed record CreateCommitmentRequest(
    string Title,
    string? Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string? Location,
    bool IsFlexible = false);

public sealed record UpdateCommitmentRequest(
    string? Title,
    string? Description,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    string? Location,
    bool? IsFlexible);

public sealed record PlanItemResponse(
    Guid Id,
    Guid? TaskId,
    Guid? CommitmentId,
    string Title,
    string? Description,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    PlanItemType Type,
    PlanItemStatus Status,
    Priority Priority);

public sealed record DailyPlanResponse(
    Guid Id,
    DateOnly Date,
    string? Summary,
    IReadOnlyList<PlanItemResponse> Items);

public sealed record CreateDailyPlanRequest(DateOnly Date, string? Summary);
public sealed record CreatePlanItemRequest(
    string Title,
    string? Description,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    PlanItemType Type,
    Priority Priority = Priority.Normal,
    Guid? TaskId = null,
    Guid? CommitmentId = null);

public sealed record PlanningContextResponse(string? TimeZone, string? PreferredWorkingHours, string? Preferences);
public sealed record UpdatePlanningContextRequest(string? TimeZone, string? PreferredWorkingHours, string? Preferences);
