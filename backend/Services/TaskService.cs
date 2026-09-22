using backend.Data;
using backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public interface ITaskService
{
    Task<IReadOnlyList<TaskResponse>> GetAllAsync(string userId, CancellationToken ct);
    Task<TaskResponse> CreateAsync(string userId, CreateTaskRequest request, CancellationToken ct);
    Task<TaskResponse?> UpdateAsync(string userId, Guid id, UpdateTaskRequest request, CancellationToken ct);
    Task<TaskResponse?> CompleteAsync(string userId, Guid id, CancellationToken ct);
}

public sealed class TaskService(AppDbContext db) : ITaskService
{
    public async Task<IReadOnlyList<TaskResponse>> GetAllAsync(string userId, CancellationToken ct) =>
        await db.Tasks.AsNoTracking().Where(task => task.UserId == userId)
            .OrderBy(task => task.Status).ThenBy(task => task.DueDate)
            .Select(task => ToResponse(task)).ToListAsync(ct);

    public async Task<TaskResponse> CreateAsync(string userId, CreateTaskRequest request, CancellationToken ct)
    {
        ValidateTitle(request.Title);
        await EnsureUserAsync(userId, ct);
        var task = new TaskItem
        {
            UserId = userId, Title = request.Title.Trim(), Description = request.Description,
            Priority = request.Priority, EstimatedMinutes = request.EstimatedMinutes, DueDate = request.DueDate
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
        return ToResponse(task);
    }

    public async Task<TaskResponse?> UpdateAsync(string userId, Guid id, UpdateTaskRequest request, CancellationToken ct)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, ct);
        if (task is null) return null;
        if (request.Title is not null) { ValidateTitle(request.Title); task.Title = request.Title.Trim(); }
        task.Description = request.Description ?? task.Description;
        task.Status = request.Status ?? task.Status;
        task.Priority = request.Priority ?? task.Priority;
        task.EstimatedMinutes = request.EstimatedMinutes ?? task.EstimatedMinutes;
        task.DueDate = request.DueDate ?? task.DueDate;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        task.CompletedAt = task.Status == backend.Domain.TaskStatus.Completed ? task.CompletedAt ?? DateTimeOffset.UtcNow : null;
        await db.SaveChangesAsync(ct);
        return ToResponse(task);
    }

    public async Task<TaskResponse?> CompleteAsync(string userId, Guid id, CancellationToken ct) =>
        await UpdateAsync(userId, id, new(null, null, backend.Domain.TaskStatus.Completed, null, null, null), ct);

    private static TaskResponse ToResponse(TaskItem task) =>
        new(task.Id, task.Title, task.Description, task.Status, task.Priority, task.EstimatedMinutes, task.DueDate, task.CompletedAt);
    private static void ValidateTitle(string title) { if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Task title is required."); }
    private async Task EnsureUserAsync(string userId, CancellationToken ct) { if (!await db.Users.AnyAsync(user => user.Id == userId, ct)) db.Users.Add(new UserProfile { Id = userId }); }
}
