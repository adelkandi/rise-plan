using backend.Calendar;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

public static class CalendarEndpoints
{
    public static void MapCalendarEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/calendar").RequireAuthorization();

        group.MapGet("/connection", async (
            HttpContext http,
            ICurrentUserService users,
            IGoogleCalendarConnectionService connections,
            CancellationToken ct) =>
        {
            var userId = users.GetRequiredUserId(http.User);
            return Results.Ok(new { provider = "Google", connected = await connections.IsConnectedAsync(userId, ct) });
        })
        .WithName("GetCalendarConnection")
        .WithSummary("Returns the current user's Google Calendar connection status.");

        group.MapGet("/connect/google", (
            HttpContext http,
            ICurrentUserService users,
            IGoogleCalendarConnectionService connections) =>
            Results.Redirect(connections.CreateAuthorizationUrl(users.GetRequiredUserId(http.User))))
        .WithName("ConnectGoogleCalendar")
        .WithSummary("Starts Google Calendar authorization.");

        group.MapDelete("/connection", async (
            HttpContext http,
            ICurrentUserService users,
            IGoogleCalendarConnectionService connections,
            CancellationToken ct) =>
        {
            await connections.DisconnectAsync(users.GetRequiredUserId(http.User), ct);
            return Results.NoContent();
        })
        .WithName("DisconnectCalendar")
        .WithSummary("Disconnects Google Calendar for the current user.");

        app.MapGet("/api/calendar/oauth/google/callback", async (
            string state,
            string code,
            IGoogleCalendarConnectionService connections,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            await connections.CompleteAuthorizationAsync(state, code, ct);
            return Results.Redirect(configuration["Google:FrontendRedirectUri"] ?? "http://localhost:5173");
        })
        .WithName("GoogleCalendarCallback")
        .WithSummary("Completes Google Calendar authorization.");

        group.MapGet("/events", async (
            DateTimeOffset from,
            DateTimeOffset to,
            HttpContext http,
            ICurrentUserService users,
            ICalendarProvider calendar,
            CancellationToken ct) =>
        {
            if (to <= from)
                return Results.BadRequest("The calendar range must end after it starts.");

            return Results.Ok(await calendar.GetEventsAsync(
                users.GetRequiredUserId(http.User), from, to, ct));
        })
        .WithName("GetCalendarEvents")
        .WithSummary("Gets calendar events in a time range.");

        group.MapPost("/events", async Task<Results<Ok<CalendarEvent>, BadRequest<ProblemHttpResult>>> (
            HttpContext http,
            ICurrentUserService users,
            ICalendarProvider calendar,
            CreateCalendarEventRequest request,
            CancellationToken ct) =>
        {
            if (!request.Confirmed)
            {
                return TypedResults.BadRequest(
                    TypedResults.Problem(
                        "Calendar creation requires explicit confirmation.",
                        statusCode: StatusCodes.Status400BadRequest));
            }

            var result = await calendar.CreateEventAsync(
                users.GetRequiredUserId(http.User), request, ct);
            return TypedResults.Ok(result);
        })
        .WithName("CreateCalendarEvent")
        .WithSummary("Creates a calendar event after explicit confirmation.")
        .Produces<CalendarEvent>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/events/{eventId}", async Task<Results<Ok<CalendarEvent>, NotFound>> (
            string eventId,
            HttpContext http,
            ICurrentUserService users,
            ICalendarProvider calendar,
            UpdateCalendarEventRequest request,
            CancellationToken ct) =>
        {
            var result = await calendar.UpdateEventAsync(
                users.GetRequiredUserId(http.User), eventId, request, ct);
            return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
        });

        group.MapDelete("/events/{eventId}", async Task<Results<NoContent, NotFound>> (
            string eventId,
            HttpContext http,
            ICurrentUserService users,
            ICalendarProvider calendar,
            CancellationToken ct) =>
            await calendar.DeleteEventAsync(
                users.GetRequiredUserId(http.User), eventId, ct)
                ? TypedResults.NoContent()
                : TypedResults.NotFound());
    }
}
