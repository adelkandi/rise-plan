using backend.Domain;
using backend.Planning;

namespace backend.Tests;

public sealed class PlanningAgentTests
{
    [Fact]
    public async Task Tentative_event_is_not_presented_as_a_task()
    {
        var agent = new DevelopmentPlanningAgent(new FakeContextReader(
            new PlanningContextSnapshot([], [], [], null)));

        var response = await agent.ProcessMessageAsync(
            "user-1",
            null,
            "I don't know if I can make it to dinner.",
            CancellationToken.None);

        Assert.Contains(response.Suggestions, item => item.Type == "decision");
        Assert.DoesNotContain(response.Suggestions, item =>
            item.Type == "task" || item.Title.Contains("create", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Overwhelmed_message_asks_which_commitment_is_fixed()
    {
        var commitment = new CommitmentSummary(
            Guid.NewGuid(),
            "Work",
            DateTimeOffset.UtcNow.AddHours(1),
            DateTimeOffset.UtcNow.AddHours(9),
            false);
        var agent = new DevelopmentPlanningAgent(new FakeContextReader(
            new PlanningContextSnapshot([], [], [commitment], null)));

        var response = await agent.ProcessMessageAsync(
            "user-1",
            null,
            "I feel overwhelmed today.",
            CancellationToken.None);

        Assert.Contains(response.Suggestions, item =>
            item.Type == "question" && item.Title == "What is fixed today?");
        Assert.Contains(response.Suggestions, item => item.Type == "summary");
    }

    [Fact]
    public async Task Open_task_context_influences_first_suggestion()
    {
        var task = new TaskSummary(Guid.NewGuid(), "Finish client proposal", Priority.High, null);
        var agent = new DevelopmentPlanningAgent(new FakeContextReader(
            new PlanningContextSnapshot([], [task], [], null)));

        var response = await agent.ProcessMessageAsync(
            "user-1",
            null,
            "What should I do first?",
            CancellationToken.None);

        var suggestion = Assert.Single(response.Suggestions);
        Assert.Contains("Finish client proposal", suggestion.Description!);
    }

    private sealed class FakeContextReader(PlanningContextSnapshot snapshot) : IPlanningContextReader
    {
        public Task<PlanningContextSnapshot> ReadAsync(string userId, Guid? conversationId, CancellationToken ct) =>
            Task.FromResult(snapshot);
    }
}
