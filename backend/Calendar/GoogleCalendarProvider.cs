using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace backend.Calendar;

public sealed class GoogleCalendarProvider(
    IHttpClientFactory httpClientFactory,
    IGoogleCalendarConnectionService connections) : ICalendarProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        string userId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var query = new Dictionary<string, string>
        {
            ["timeMin"] = from.UtcDateTime.ToString("O"),
            ["timeMax"] = to.UtcDateTime.ToString("O"),
            ["singleEvents"] = "true",
            ["orderBy"] = "startTime"
        };
        var payload = await SendAsync<GoogleEventsResponse>(
            userId,
            HttpMethod.Get,
            $"calendars/primary/events?{string.Join("&", query.Select(item => $"{item.Key}={Uri.EscapeDataString(item.Value)}"))}",
            null,
            ct);

        return payload.Items
            .Select(item => ToCalendarEvent(userId, item))
            .Where(item => item is not null)
            .Cast<CalendarEvent>()
            .ToList();
    }

    public async Task<CalendarEvent> CreateEventAsync(
        string userId,
        CreateCalendarEventRequest request,
        CancellationToken ct)
    {
        Validate(request.Title, request.StartTime, request.EndTime);
        var payload = await SendAsync<GoogleEvent>(
            userId,
            HttpMethod.Post,
            "calendars/primary/events",
            new
            {
                summary = request.Title.Trim(),
                description = request.Description,
                location = request.Location,
                start = new { dateTime = request.StartTime.UtcDateTime.ToString("O"), timeZone = "UTC" },
                end = new { dateTime = request.EndTime.UtcDateTime.ToString("O"), timeZone = "UTC" }
            },
            ct);

        return ToCalendarEvent(userId, payload)
            ?? throw new InvalidOperationException("Google returned an event without start or end times.");
    }

    public async Task<CalendarEvent?> UpdateEventAsync(
        string userId,
        string eventId,
        UpdateCalendarEventRequest request,
        CancellationToken ct)
    {
        var existing = await SendAsync<GoogleEvent>(userId, HttpMethod.Get, $"calendars/primary/events/{Uri.EscapeDataString(eventId)}", null, ct);
        var start = request.StartTime ?? ParseDate(existing.Start);
        var end = request.EndTime ?? ParseDate(existing.End);
        var title = request.Title ?? existing.Summary ?? "Untitled event";
        Validate(title, start, end);
        var payload = await SendAsync<GoogleEvent>(
            userId,
            HttpMethod.Put,
            $"calendars/primary/events/{Uri.EscapeDataString(eventId)}",
            new
            {
                summary = title,
                description = request.Description ?? existing.Description,
                location = request.Location ?? existing.Location,
                start = new { dateTime = start.UtcDateTime.ToString("O"), timeZone = "UTC" },
                end = new { dateTime = end.UtcDateTime.ToString("O"), timeZone = "UTC" }
            },
            ct);

        return ToCalendarEvent(userId, payload);
    }

    public async Task<bool> DeleteEventAsync(string userId, string eventId, CancellationToken ct)
    {
        using var response = await SendRawAsync(
            userId,
            HttpMethod.Delete,
            $"calendars/primary/events/{Uri.EscapeDataString(eventId)}",
            null,
            ct);
        return response.IsSuccessStatusCode;
    }

    private async Task<T> SendAsync<T>(string userId, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var response = await SendRawAsync(userId, method, path, body, ct);
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("Google Calendar returned an empty response.");
    }

    private async Task<HttpResponseMessage> SendRawAsync(string userId, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var token = await connections.GetAccessTokenAsync(userId, ct)
            ?? throw new InvalidOperationException("Google Calendar is not connected for this user.");
        var client = httpClientFactory.CreateClient("GoogleCalendar");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode();
        return response;
    }


    private static CalendarEvent? ToCalendarEvent(string userId, GoogleEvent item)
    {
        if (item.Start?.DateTime is null || item.End?.DateTime is null || item.Id is null)
            return null;
        return new(item.Id, userId, item.Summary ?? "Untitled event", item.Description,
            item.Start.DateTime.Value, item.End.DateTime.Value, item.Location);
    }

    private static DateTimeOffset ParseDate(GoogleEventTime? value) =>
        value?.DateTime ?? throw new InvalidOperationException("Google event has no timed start or end.");

    private static void Validate(string title, DateTimeOffset start, DateTimeOffset end)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Calendar event title is required.");
        if (end <= start)
            throw new ArgumentException("Calendar event end time must be after its start time.");
    }

    private sealed record GoogleEventsResponse(IReadOnlyList<GoogleEvent> Items);
    private sealed record GoogleEvent(
        string? Id,
        string? Summary,
        string? Description,
        string? Location,
        GoogleEventTime? Start,
        GoogleEventTime? End);
    private sealed record GoogleEventTime(DateTimeOffset? DateTime, string? Date);
}
