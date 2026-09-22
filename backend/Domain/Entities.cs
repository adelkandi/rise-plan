namespace backend.Domain;

public sealed class UserProfile
{
    public required string Id { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Conversation> Conversations { get; } = [];
    public ICollection<TaskItem> Tasks { get; } = [];
    public ICollection<Commitment> Commitments { get; } = [];
    public ICollection<DailyPlan> DailyPlans { get; } = [];
    public UserPlanningContext? PlanningContext { get; set; }
}

public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }
    public string? Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public UserProfile User { get; set; } = null!;
    public ICollection<Message> Messages { get; } = [];
}

public sealed class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public required MessageRole Role { get; set; }
    public required string Content { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Metadata { get; set; }
    public Conversation Conversation { get; set; } = null!;
}

public sealed class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Open;
    public Priority Priority { get; set; } = Priority.Normal;
    public int? EstimatedMinutes { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public UserProfile User { get; set; } = null!;
}

public sealed class Commitment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public string? Location { get; set; }
    public bool IsFlexible { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public UserProfile User { get; set; } = null!;
}

public sealed class DailyPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }
    public DateOnly Date { get; set; }
    public string? Summary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public UserProfile User { get; set; } = null!;
    public ICollection<PlanItem> Items { get; } = [];
}

public sealed class PlanItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DailyPlanId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? CommitmentId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public PlanItemType Type { get; set; }
    public PlanItemStatus Status { get; set; } = PlanItemStatus.Planned;
    public Priority Priority { get; set; } = Priority.Normal;
    public DailyPlan DailyPlan { get; set; } = null!;
    public TaskItem? Task { get; set; }
    public Commitment? Commitment { get; set; }
}

public sealed class UserPlanningContext
{
    public required string UserId { get; set; }
    public string? TimeZone { get; set; }
    public string? PreferredWorkingHours { get; set; }
    public string? Preferences { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public UserProfile User { get; set; } = null!;
}
