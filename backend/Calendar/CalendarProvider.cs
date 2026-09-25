namespace backend.Calendar;

public interface ICalendarProvider
{
    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        string userId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct);

    Task<CalendarEvent> CreateEventAsync(
        string userId,
        CreateCalendarEventRequest request,
        CancellationToken ct);

    Task<CalendarEvent?> UpdateEventAsync(
        string userId,
        string eventId,
        UpdateCalendarEventRequest request,
        CancellationToken ct);

    Task<bool> DeleteEventAsync(string userId, string eventId, CancellationToken ct);
}

public sealed class DevelopmentCalendarProvider : ICalendarProvider
{
    private readonly Dictionary<string, CalendarEvent> events = [];
    private readonly object sync = new();

    public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        string userId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        lock (sync)
        {
            IReadOnlyList<CalendarEvent> result = events.Values
                .Where(item => item.UserId == userId && item.EndTime >= from && item.StartTime <= to)
                .OrderBy(item => item.StartTime)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<CalendarEvent> CreateEventAsync(
        string userId,
        CreateCalendarEventRequest request,
        CancellationToken ct)
    {
        Validate(request.Title, request.StartTime, request.EndTime);
        var calendarEvent = new CalendarEvent(
            Guid.NewGuid().ToString("N"),
            userId,
            request.Title.Trim(),
            request.Description,
            request.StartTime,
            request.EndTime,
            request.Location);

        lock (sync)
        {
            events[calendarEvent.Id] = calendarEvent;
        }

        return Task.FromResult(calendarEvent);
    }

    public Task<CalendarEvent?> UpdateEventAsync(
        string userId,
        string eventId,
        UpdateCalendarEventRequest request,
        CancellationToken ct)
    {
        lock (sync)
        {
            if (!events.TryGetValue(eventId, out var current) || current.UserId != userId)
                return Task.FromResult<CalendarEvent?>(null);

            var updated = current with
            {
                Title = request.Title?.Trim() ?? current.Title,
                Description = request.Description ?? current.Description,
                StartTime = request.StartTime ?? current.StartTime,
                EndTime = request.EndTime ?? current.EndTime,
                Location = request.Location ?? current.Location
            };
            Validate(updated.Title, updated.StartTime, updated.EndTime);
            events[eventId] = updated;
            return Task.FromResult<CalendarEvent?>(updated);
        }
    }

    public Task<bool> DeleteEventAsync(string userId, string eventId, CancellationToken ct)
    {
        lock (sync)
        {
            if (!events.TryGetValue(eventId, out var current) || current.UserId != userId)
                return Task.FromResult(false);

            events.Remove(eventId);
            return Task.FromResult(true);
        }
    }

    private static void Validate(string title, DateTimeOffset start, DateTimeOffset end)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Calendar event title is required.");
        if (end <= start)
            throw new ArgumentException("Calendar event end time must be after its start time.");
    }
}
