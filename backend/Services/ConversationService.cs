using backend.Data;
using backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public interface IConversationService
{
    Task<IReadOnlyList<ConversationResponse>> GetAllAsync(string userId, CancellationToken ct);
    Task<ConversationDetailsResponse?> GetAsync(string userId, Guid conversationId, CancellationToken ct);
    Task<ConversationResponse> CreateAsync(string userId, CreateConversationRequest request, CancellationToken ct);
    Task<MessageResponse?> AddMessageAsync(string userId, Guid conversationId, CreateMessageRequest request, CancellationToken ct);
}

public sealed class ConversationService(AppDbContext db) : IConversationService
{
    public async Task<IReadOnlyList<ConversationResponse>> GetAllAsync(string userId, CancellationToken ct) =>
        await db.Conversations.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => new ConversationResponse(item.Id, item.Title, item.CreatedAt, item.UpdatedAt))
            .ToListAsync(ct);

    public async Task<ConversationDetailsResponse?> GetAsync(string userId, Guid conversationId, CancellationToken ct)
    {
        var conversation = await db.Conversations.AsNoTracking()
            .Include(item => item.Messages.OrderBy(message => message.CreatedAt))
            .SingleOrDefaultAsync(item => item.Id == conversationId && item.UserId == userId, ct);

        return conversation is null
            ? null
            : new ConversationDetailsResponse(
                new ConversationResponse(conversation.Id, conversation.Title, conversation.CreatedAt, conversation.UpdatedAt),
                conversation.Messages.Select(message => new MessageResponse(message.Id, message.Role, message.Content, message.CreatedAt)).ToList());
    }

    public async Task<ConversationResponse> CreateAsync(string userId, CreateConversationRequest request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var conversation = new Conversation { UserId = userId, Title = request.Title, CreatedAt = now, UpdatedAt = now };
        await EnsureUserAsync(userId, ct);
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(ct);
        return new(conversation.Id, conversation.Title, conversation.CreatedAt, conversation.UpdatedAt);
    }

    public async Task<MessageResponse?> AddMessageAsync(string userId, Guid conversationId, CreateMessageRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Message content is required.", nameof(request));

        var conversation = await db.Conversations.SingleOrDefaultAsync(
            item => item.Id == conversationId && item.UserId == userId, ct);
        if (conversation is null)
            return null;

        var message = new Message { ConversationId = conversationId, Role = MessageRole.User, Content = request.Content.Trim() };
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);
        return new(message.Id, message.Role, message.Content, message.CreatedAt);
    }

    private async Task EnsureUserAsync(string userId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(user => user.Id == userId, ct))
            db.Users.Add(new UserProfile { Id = userId });
    }
}
