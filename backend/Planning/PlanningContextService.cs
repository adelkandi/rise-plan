using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Planning;

public interface IPlanningContextReader
{
    Task<PlanningContextSnapshot> ReadAsync(string userId, Guid? conversationId, CancellationToken ct);
}

public sealed class PlanningContextReader(AppDbContext db) : IPlanningContextReader
{
    public async Task<PlanningContextSnapshot> ReadAsync(string userId, Guid? conversationId, CancellationToken ct)
    {
        var recentMessages = conversationId is null
            ? []
            : await db.Messages.AsNoTracking()
                .Where(message => message.ConversationId == conversationId &&
                                  message.Conversation.UserId == userId)
                .OrderByDescending(message => message.CreatedAt)
                .Take(20)
                .OrderBy(message => message.CreatedAt)
                .Select(message => $"{message.Role}: {message.Content}")
                .ToListAsync(ct);

        var openTasks = await db.Tasks.AsNoTracking()
            .Where(task => task.UserId == userId && task.Status != Domain.TaskStatus.Completed &&
                           task.Status != Domain.TaskStatus.Cancelled)
            .OrderBy(task => task.DueDate)
            .Take(20)
            .Select(task => new TaskSummary(task.Id, task.Title, task.Priority, task.DueDate))
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var commitments = await db.Commitments.AsNoTracking()
            .Where(item => item.UserId == userId && item.EndTime >= now)
            .OrderBy(item => item.StartTime)
            .Take(20)
            .Select(item => new CommitmentSummary(item.Id, item.Title, item.StartTime, item.EndTime, item.IsFlexible))
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = await db.DailyPlans.AsNoTracking()
            .Where(item => item.UserId == userId && item.Date == today)
            .Select(item => new DailyPlanSummary(item.Id, item.Date, item.Summary, item.Items.Count))
            .SingleOrDefaultAsync(ct);

        return new(recentMessages, openTasks, commitments, plan);
    }
}
