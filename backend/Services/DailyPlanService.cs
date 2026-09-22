using backend.Data;
using backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public interface IDailyPlanService
{
    Task<DailyPlanResponse?> GetAsync(string userId, DateOnly date, CancellationToken ct);
    Task<DailyPlanResponse> CreateAsync(string userId, CreateDailyPlanRequest request, CancellationToken ct);
    Task<PlanItemResponse?> AddItemAsync(string userId, DateOnly date, CreatePlanItemRequest request, CancellationToken ct);
}

public sealed class DailyPlanService(AppDbContext db) : IDailyPlanService
{
    public async Task<DailyPlanResponse?> GetAsync(string userId, DateOnly date, CancellationToken ct)
    {
        var plan = await db.DailyPlans.AsNoTracking().Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Date == date, ct);
        return plan is null ? null : ToResponse(plan);
    }

    public async Task<DailyPlanResponse> CreateAsync(string userId, CreateDailyPlanRequest request, CancellationToken ct)
    {
        var plan = await db.DailyPlans.Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Date == request.Date, ct);
        if (plan is null)
        {
            if (!await db.Users.AnyAsync(user => user.Id == userId, ct)) db.Users.Add(new UserProfile { Id = userId });
            plan = new DailyPlan { UserId = userId, Date = request.Date, Summary = request.Summary };
            db.DailyPlans.Add(plan);
        }
        else
        {
            plan.Summary = request.Summary ?? plan.Summary;
            plan.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return ToResponse(plan);
    }

    public async Task<PlanItemResponse?> AddItemAsync(string userId, DateOnly date, CreatePlanItemRequest request, CancellationToken ct)
    {
        var plan = await db.DailyPlans.Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Date == date, ct);
        if (plan is null) return null;
        if (request.TaskId.HasValue && !await db.Tasks.AnyAsync(item => item.Id == request.TaskId && item.UserId == userId, ct))
            throw new KeyNotFoundException("The selected task was not found.");
        if (request.CommitmentId.HasValue && !await db.Commitments.AnyAsync(item => item.Id == request.CommitmentId && item.UserId == userId, ct))
            throw new KeyNotFoundException("The selected commitment was not found.");
        var item = new PlanItem
        {
            DailyPlanId = plan.Id, TaskId = request.TaskId, CommitmentId = request.CommitmentId,
            Title = request.Title.Trim(), Description = request.Description, StartTime = request.StartTime,
            EndTime = request.EndTime, Type = request.Type, Priority = request.Priority
        };
        plan.UpdatedAt = DateTimeOffset.UtcNow;
        db.PlanItems.Add(item);
        await db.SaveChangesAsync(ct);
        return ToResponse(item);
    }

    private static DailyPlanResponse ToResponse(DailyPlan plan) =>
        new(plan.Id, plan.Date, plan.Summary, plan.Items.OrderBy(item => item.StartTime).Select(ToResponse).ToList());
    private static PlanItemResponse ToResponse(PlanItem item) =>
        new(item.Id, item.TaskId, item.CommitmentId, item.Title, item.Description, item.StartTime, item.EndTime, item.Type, item.Status, item.Priority);
}
