using backend.Calendar;
using backend.Data;
using backend.Domain;
using backend.Services;
using Microsoft.EntityFrameworkCore;

namespace backend.Planning;

public interface IDailyPlanningService
{
    Task<DailyPlanResponse> BuildPlanAsync(string userId, DateOnly date, CancellationToken ct);
}

public sealed class DailyPlanningService(
    AppDbContext db,
    ICalendarProvider calendar) : IDailyPlanningService
{
    public async Task<DailyPlanResponse> BuildPlanAsync(string userId, DateOnly date, CancellationToken ct)
    {
        var context = await db.UserPlanningContexts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, ct);
        var timeZone = ResolveTimeZone(context?.TimeZone);
        var dayStart = ToOffset(date, new TimeOnly(0, 0), timeZone);
        var dayEnd = dayStart.AddDays(1);
        var workingHours = ParseWorkingHours(context?.PreferredWorkingHours);

        var tasks = await db.Tasks
            .Where(item => item.UserId == userId &&
                           item.Status != backend.Domain.TaskStatus.Completed &&
                           item.Status != backend.Domain.TaskStatus.Cancelled)
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.DueDate ?? DateTimeOffset.MaxValue)
            .ToListAsync(ct);
        var commitments = await db.Commitments
            .Where(item => item.UserId == userId &&
                           item.EndTime > dayStart && item.StartTime < dayEnd)
            .OrderBy(item => item.StartTime)
            .ToListAsync(ct);
        var events = await calendar.GetEventsAsync(userId, dayStart, dayEnd, ct);

        var plan = await db.DailyPlans
            .Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Date == date, ct);
        if (plan is null)
        {
            if (!await db.Users.AnyAsync(item => item.Id == userId, ct))
                db.Users.Add(new UserProfile { Id = userId });
            plan = new DailyPlan { UserId = userId, Date = date };
            db.DailyPlans.Add(plan);
        }
        else
        {
            db.PlanItems.RemoveRange(plan.Items);
            plan.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var fixedItems = commitments
            .Where(item => !item.IsFlexible)
            .Select(item => new ScheduledItem(
                item.Title, item.Description, item.StartTime, item.EndTime,
                PlanItemType.Commitment, Priority.High, null, item.Id))
            .Concat(events.Select(item => new ScheduledItem(
                item.Title, item.Description, item.StartTime, item.EndTime,
                PlanItemType.Activity, Priority.High, null, null)))
            .OrderBy(item => item.StartTime)
            .ToList();

        foreach (var item in fixedItems)
        {
            plan.Items.Add(new PlanItem
            {
                Title = item.Title,
                Description = item.Description,
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                Type = item.Type,
                Priority = item.Priority,
                CommitmentId = item.CommitmentId
            });
        }

        var occupied = fixedItems
            .Select(item => (Start: Max(item.StartTime, dayStart), End: Min(item.EndTime, dayEnd)))
            .Where(item => item.End > item.Start)
            .OrderBy(item => item.Start)
            .ToList();
        var cursor = ToOffset(date, workingHours.Start, timeZone);
        var workEnd = ToOffset(date, workingHours.End, timeZone);
        var unscheduled = 0;

        foreach (var task in tasks)
        {
            var duration = TimeSpan.FromMinutes(Math.Clamp(task.EstimatedMinutes ?? 30, 15, 240));
            var slot = FindSlot(cursor, workEnd, duration, occupied);
            if (slot is null)
            {
                unscheduled++;
                continue;
            }

            plan.Items.Add(new PlanItem
            {
                TaskId = task.Id,
                Title = task.Title,
                Description = task.Description,
                StartTime = slot.Value.Start,
                EndTime = slot.Value.End,
                Type = PlanItemType.Task,
                Priority = task.Priority
            });
            occupied.Add((slot.Value.Start, slot.Value.End));
            occupied = occupied.OrderBy(item => item.Start).ToList();
            cursor = slot.Value.End.AddMinutes(15);
        }

        plan.Summary = unscheduled == 0
            ? $"A realistic plan with {tasks.Count} task(s) around your fixed commitments."
            : $"{unscheduled} task(s) did not fit today and remain unscheduled.";
        plan.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return new DailyPlanResponse(
            plan.Id,
            plan.Date,
            plan.Summary,
            plan.Items.OrderBy(item => item.StartTime).Select(ToResponse).ToList());
    }

    private static (TimeOnly Start, TimeOnly End) ParseWorkingHours(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var parts = value.Split('-', StringSplitOptions.TrimEntries);
            if (parts.Length == 2 &&
                TimeOnly.TryParse(parts[0], out var start) &&
                TimeOnly.TryParse(parts[1], out var end) &&
                end > start)
                return (start, end);
        }
        return (new TimeOnly(9, 0), new TimeOnly(17, 0));
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }

    private static DateTimeOffset ToOffset(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        var offset = zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left > right ? left : right;
    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;

    private static (DateTimeOffset Start, DateTimeOffset End)? FindSlot(
        DateTimeOffset cursor,
        DateTimeOffset workEnd,
        TimeSpan duration,
        IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> occupied)
    {
        var candidate = cursor;
        foreach (var item in occupied)
        {
            if (item.End <= candidate)
                continue;
            if (item.Start - candidate >= duration)
                return (candidate, candidate.Add(duration));
            candidate = Max(candidate, item.End).AddMinutes(15);
        }
        return workEnd - candidate >= duration ? (candidate, candidate.Add(duration)) : null;
    }

    private static PlanItemResponse ToResponse(PlanItem item) =>
        new(item.Id, item.TaskId, item.CommitmentId, item.Title, item.Description,
            item.StartTime, item.EndTime, item.Type, item.Status, item.Priority);

    private sealed record ScheduledItem(
        string Title,
        string? Description,
        DateTimeOffset StartTime,
        DateTimeOffset EndTime,
        PlanItemType Type,
        Priority Priority,
        Guid? TaskId,
        Guid? CommitmentId);
}
