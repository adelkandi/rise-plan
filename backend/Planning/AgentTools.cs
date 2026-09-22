using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Services;

namespace backend.Planning;

public sealed record AgentToolDefinition(string Name, string Description);
public sealed record AgentToolCall(string Name, JsonElement Arguments);
public sealed record AgentToolResult(string Name, bool Succeeded, object? Data, string? Error);

public interface IAgentToolExecutor
{
    IReadOnlyList<AgentToolDefinition> GetDefinitions();
    Task<AgentToolResult> ExecuteAsync(string userId, AgentToolCall call, CancellationToken ct);
}

public sealed class AgentToolExecutor(
    ITaskService tasks,
    ICommitmentService commitments,
    IDailyPlanService plans,
    IPlanningContextReader contextReader) : IAgentToolExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public IReadOnlyList<AgentToolDefinition> GetDefinitions() =>
    [
        new("get_today_context", "Read the user's bounded planning context."),
        new("get_tasks", "List the user's tasks."),
        new("create_task", "Create a task only when the user explicitly wants one."),
        new("update_task", "Update an existing user task."),
        new("complete_task", "Mark an existing user task complete."),
        new("get_commitments", "List the user's commitments."),
        new("create_commitment", "Create a user commitment."),
        new("update_commitment", "Update an existing commitment."),
        new("get_daily_plan", "Read the user's daily plan."),
        new("create_daily_plan", "Create or update a daily plan.")
    ];

    public async Task<AgentToolResult> ExecuteAsync(string userId, AgentToolCall call, CancellationToken ct)
    {
        try
        {
            object? result = call.Name switch
            {
                "get_today_context" => await contextReader.ReadAsync(userId, null, ct),
                "get_tasks" => await tasks.GetAllAsync(userId, ct),
                "create_task" => await tasks.CreateAsync(userId, Read<CreateTaskRequest>(call), ct),
                "update_task" => await UpdateTaskAsync(userId, call, ct),
                "complete_task" => await tasks.CompleteAsync(userId, ReadId(call), ct)
                    ?? throw new KeyNotFoundException("Task not found."),
                "get_commitments" => await GetCommitmentsAsync(userId, call, ct),
                "create_commitment" => await commitments.CreateAsync(userId, Read<CreateCommitmentRequest>(call), ct),
                "update_commitment" => await UpdateCommitmentAsync(userId, call, ct),
                "get_daily_plan" => await GetPlanAsync(userId, call, ct),
                "create_daily_plan" => await plans.CreateAsync(userId, Read<CreateDailyPlanRequest>(call), ct),
                _ => throw new ArgumentException($"Unknown agent tool '{call.Name}'.")
            };

            return new(call.Name, true, result, null);
        }
        catch (JsonException exception)
        {
            return new(call.Name, false, null, $"Invalid arguments: {exception.Message}");
        }
        catch (ArgumentException exception)
        {
            return new(call.Name, false, null, exception.Message);
        }
        catch (KeyNotFoundException exception)
        {
            return new(call.Name, false, null, exception.Message);
        }
    }

    private async Task<TaskResponse> UpdateTaskAsync(string userId, AgentToolCall call, CancellationToken ct) =>
        await tasks.UpdateAsync(userId, ReadId(call), Read<UpdateTaskRequest>(call), ct)
        ?? throw new KeyNotFoundException("Task not found.");

    private async Task<CommitmentResponse> UpdateCommitmentAsync(string userId, AgentToolCall call, CancellationToken ct) =>
        await commitments.UpdateAsync(userId, ReadId(call), Read<UpdateCommitmentRequest>(call), ct)
        ?? throw new KeyNotFoundException("Commitment not found.");

    private async Task<IReadOnlyList<CommitmentResponse>> GetCommitmentsAsync(string userId, AgentToolCall call, CancellationToken ct)
    {
        var arguments = Read<GetCommitmentsArguments>(call);
        return await commitments.GetAllAsync(userId, arguments.From, arguments.To, ct);
    }

    private async Task<DailyPlanResponse?> GetPlanAsync(string userId, AgentToolCall call, CancellationToken ct)
    {
        var arguments = Read<GetDailyPlanArguments>(call);
        return await plans.GetAsync(userId, arguments.Date, ct);
    }

    private static Guid ReadId(AgentToolCall call)
    {
        var arguments = Read<IdArguments>(call);
        return arguments.Id == Guid.Empty ? throw new ArgumentException("A valid id is required.") : arguments.Id;
    }

    private static T Read<T>(AgentToolCall call) =>
        JsonSerializer.Deserialize<T>(call.Arguments.GetRawText(), JsonOptions)
        ?? throw new JsonException("Arguments cannot be empty.");

    private sealed record IdArguments(Guid Id);
    private sealed record GetCommitmentsArguments(DateTimeOffset? From, DateTimeOffset? To);
    private sealed record GetDailyPlanArguments(DateOnly Date);
}
