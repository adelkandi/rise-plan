namespace backend.Domain;

public enum MessageRole
{
    User,
    Assistant,
    System,
    Tool
}

public enum TaskStatus
{
    Open,
    InProgress,
    Completed,
    Cancelled
}

public enum Priority
{
    Low,
    Normal,
    High
}

public enum PlanItemType
{
    Task,
    Commitment,
    Activity,
    Break,
    Travel,
    Decision
}

public enum PlanItemStatus
{
    Planned,
    InProgress,
    Completed,
    Skipped
}
