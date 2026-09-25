namespace backend.Calendar;

public sealed record CalendarEvent(
    string Id,
    string UserId,
    string Title,
    string? Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string? Location);

public sealed record CreateCalendarEventRequest(
    string Title,
    string? Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string? Location,
    bool Confirmed);

public sealed record UpdateCalendarEventRequest(
    string? Title,
    string? Description,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    string? Location);
