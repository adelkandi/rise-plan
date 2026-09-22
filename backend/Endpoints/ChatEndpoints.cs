using backend.Planning;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        app.MapPost("/api/chat/message",
            async Task<Results<Ok<ChatMessageResponse>, BadRequest<ProblemHttpResult>>> (
                HttpContext http,
                ICurrentUserService users,
                IPlanningAgent agent,
                IConversationService conversations,
                ChatMessageRequest request,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return TypedResults.BadRequest(
                        TypedResults.Problem("Message is required.", statusCode: StatusCodes.Status400BadRequest));
                }

                var userId = users.GetRequiredUserId(http.User);
                var conversationId = request.ConversationId;
                if (conversationId is null)
                {
                    var conversation = await conversations.CreateAsync(
                        userId,
                        new CreateConversationRequest(request.Message[..Math.Min(request.Message.Length, 80)]),
                        ct);
                    conversationId = conversation.Id;
                }

                if (await conversations.AddMessageAsync(
                        userId,
                        conversationId.Value,
                        new CreateMessageRequest(request.Message.Trim()),
                        ct) is null)
                {
                    return TypedResults.BadRequest(
                        TypedResults.Problem("The conversation was not found.", statusCode: StatusCodes.Status400BadRequest));
                }

                var response = await agent.ProcessMessageAsync(
                    userId,
                    conversationId,
                    request.Message.Trim(),
                    ct);

                await conversations.AddAssistantMessageAsync(
                    userId,
                    conversationId.Value,
                    response.Content,
                    ct);

                return TypedResults.Ok(new ChatMessageResponse(conversationId.Value, response));
            })
            .RequireAuthorization()
            .WithName("ProcessChatMessage")
            .WithSummary("Processes a message through the planning agent.")
            .WithDescription("Reads bounded user context and returns a structured planning response.")
            .Produces<PlanningResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
