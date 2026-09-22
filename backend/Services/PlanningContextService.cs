using backend.Data;
using backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public interface IPlanningContextService
{
    Task<PlanningContextResponse> GetAsync(string userId, CancellationToken ct);
    Task<PlanningContextResponse> UpdateAsync(string userId, UpdatePlanningContextRequest request, CancellationToken ct);
}

public sealed class PlanningContextService(AppDbContext db) : IPlanningContextService
{
    public async Task<PlanningContextResponse> GetAsync(string userId, CancellationToken ct)
    {
        var context = await db.UserPlanningContexts.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, ct);
        return context is null ? new(null, null, null) : ToResponse(context);
    }

    public async Task<PlanningContextResponse> UpdateAsync(string userId, UpdatePlanningContextRequest request, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([userId], ct) ?? new UserProfile { Id = userId };
        if (db.Entry(user).State == EntityState.Detached) db.Users.Add(user);
        var context = await db.UserPlanningContexts.SingleOrDefaultAsync(item => item.UserId == userId, ct)
            ?? new UserPlanningContext { UserId = userId };
        context.TimeZone = request.TimeZone; context.PreferredWorkingHours = request.PreferredWorkingHours;
        context.Preferences = request.Preferences; context.UpdatedAt = DateTimeOffset.UtcNow;
        if (db.Entry(context).State == EntityState.Detached) db.UserPlanningContexts.Add(context);
        await db.SaveChangesAsync(ct);
        return ToResponse(context);
    }

    private static PlanningContextResponse ToResponse(UserPlanningContext context) => new(context.TimeZone, context.PreferredWorkingHours, context.Preferences);
}
