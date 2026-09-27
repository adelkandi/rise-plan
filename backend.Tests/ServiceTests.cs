using backend.Data;
using backend.Domain;
using backend.Calendar;
using backend.Planning;
using backend.Services;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

public sealed class ServiceTests
{
    [Fact]
    public async Task Task_service_does_not_update_another_users_task()
    {
        await using var db = CreateDb();
        var task = new TaskItem { UserId = "user-1", Title = "Private task" };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();
        var service = new TaskService(db);

        var result = await service.UpdateAsync(
            "user-2",
            task.Id,
            new UpdateTaskRequest("Changed", null, null, null, null, null),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal("Private task", (await db.Tasks.FindAsync(task.Id))!.Title);
    }

    [Fact]
    public async Task Commitment_service_rejects_end_before_start()
    {
        await using var db = CreateDb();
        var service = new CommitmentService(db);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(
                "user-1",
                new CreateCommitmentRequest(
                    "Invalid",
                    null,
                    DateTimeOffset.UtcNow.AddHours(2),
                    DateTimeOffset.UtcNow.AddHours(1),
                    null),
                CancellationToken.None));

        Assert.Contains("after its start", exception.Message);
    }

    [Fact]
    public async Task Daily_planning_service_keeps_tasks_out_of_commitments()
    {
        await using var db = CreateDb();
        db.Users.Add(new UserProfile { Id = "user-1" });
        db.Commitments.Add(new Commitment
        {
            UserId = "user-1",
            Title = "Work",
            StartTime = new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero),
            EndTime = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero)
        });
        db.Tasks.Add(new TaskItem
        {
            UserId = "user-1",
            Title = "Write report",
            EstimatedMinutes = 60,
            Priority = Priority.High
        });
        await db.SaveChangesAsync();

        var service = new DailyPlanningService(db, new DevelopmentCalendarProvider());
        var plan = await service.BuildPlanAsync("user-1", new DateOnly(2026, 9, 27), CancellationToken.None);
        var taskItem = Assert.Single(plan.Items, item => item.TaskId is not null);

        Assert.True(taskItem.EndTime <= new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero) ||
                    taskItem.StartTime >= new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero));
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
