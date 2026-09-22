using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

public static class ApplicationEndpoints
{
    public static void MapApplicationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        group.MapGet("/conversations", async (HttpContext http, ICurrentUserService users, IConversationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAllAsync(users.GetRequiredUserId(http.User), ct)));
        group.MapPost("/conversations", async (HttpContext http, ICurrentUserService users, IConversationService service, CreateConversationRequest request, CancellationToken ct) =>
            TypedResults.Created("/api/conversations", await service.CreateAsync(users.GetRequiredUserId(http.User), request, ct)));
        group.MapGet("/conversations/{id:guid}", async Task<Results<Ok<ConversationDetailsResponse>, NotFound>> (Guid id, HttpContext http, ICurrentUserService users, IConversationService service, CancellationToken ct) =>
            (await service.GetAsync(users.GetRequiredUserId(http.User), id, ct)) is { } result ? TypedResults.Ok(result) : TypedResults.NotFound());
        group.MapPost("/conversations/{id:guid}/messages", async Task<Results<Ok<MessageResponse>, NotFound>> (Guid id, HttpContext http, ICurrentUserService users, IConversationService service, CreateMessageRequest request, CancellationToken ct) =>
            (await service.AddMessageAsync(users.GetRequiredUserId(http.User), id, request, ct)) is { } result ? TypedResults.Ok(result) : TypedResults.NotFound());

        group.MapGet("/tasks", async (HttpContext http, ICurrentUserService users, ITaskService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAllAsync(users.GetRequiredUserId(http.User), ct)));
        group.MapPost("/tasks", async (HttpContext http, ICurrentUserService users, ITaskService service, CreateTaskRequest request, CancellationToken ct) =>
            TypedResults.Created("/api/tasks", await service.CreateAsync(users.GetRequiredUserId(http.User), request, ct)));
        group.MapPut("/tasks/{id:guid}", async Task<Results<Ok<TaskResponse>, NotFound>> (Guid id, HttpContext http, ICurrentUserService users, ITaskService service, UpdateTaskRequest request, CancellationToken ct) =>
            (await service.UpdateAsync(users.GetRequiredUserId(http.User), id, request, ct)) is { } result ? TypedResults.Ok(result) : TypedResults.NotFound());
        group.MapPost("/tasks/{id:guid}/complete", async Task<Results<Ok<TaskResponse>, NotFound>> (Guid id, HttpContext http, ICurrentUserService users, ITaskService service, CancellationToken ct) =>
            (await service.CompleteAsync(users.GetRequiredUserId(http.User), id, ct)) is { } result ? TypedResults.Ok(result) : TypedResults.NotFound());

        group.MapGet("/commitments", async (DateTimeOffset? from, DateTimeOffset? to, HttpContext http, ICurrentUserService users, ICommitmentService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAllAsync(users.GetRequiredUserId(http.User), from, to, ct)));
        group.MapPost("/commitments", async (HttpContext http, ICurrentUserService users, ICommitmentService service, CreateCommitmentRequest request, CancellationToken ct) =>
            TypedResults.Created("/api/commitments", await service.CreateAsync(users.GetRequiredUserId(http.User), request, ct)));
        group.MapPut("/commitments/{id:guid}", async Task<Results<Ok<CommitmentResponse>, NotFound>> (Guid id, HttpContext http, ICurrentUserService users, ICommitmentService service, UpdateCommitmentRequest request, CancellationToken ct) =>
            (await service.UpdateAsync(users.GetRequiredUserId(http.User), id, request, ct)) is { } result ? TypedResults.Ok(result) : TypedResults.NotFound());

        group.MapGet("/plans/{date}", async (DateOnly date, HttpContext http, ICurrentUserService users, IDailyPlanService service, CancellationToken ct) =>
            (await service.GetAsync(users.GetRequiredUserId(http.User), date, ct)) is { } result ? Results.Ok(result) : Results.NotFound());
        group.MapPut("/plans/{date}", async (DateOnly date, HttpContext http, ICurrentUserService users, IDailyPlanService service, CreateDailyPlanRequest request, CancellationToken ct) =>
            TypedResults.Ok(await service.CreateAsync(users.GetRequiredUserId(http.User), request with { Date = date }, ct)));
        group.MapPost("/plans/{date}/items", async Task<Results<Ok<PlanItemResponse>, NotFound>> (DateOnly date, HttpContext http, ICurrentUserService users, IDailyPlanService service, CreatePlanItemRequest request, CancellationToken ct) =>
            (await service.AddItemAsync(users.GetRequiredUserId(http.User), date, request, ct)) is { } result ? TypedResults.Ok(result) : TypedResults.NotFound());

        group.MapGet("/planning-context", async (HttpContext http, ICurrentUserService users, IPlanningContextService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(users.GetRequiredUserId(http.User), ct)));
        group.MapPut("/planning-context", async (HttpContext http, ICurrentUserService users, IPlanningContextService service, UpdatePlanningContextRequest request, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateAsync(users.GetRequiredUserId(http.User), request, ct)));
    }
}
