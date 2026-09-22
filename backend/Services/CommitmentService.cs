using backend.Data;
using backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public interface ICommitmentService
{
    Task<IReadOnlyList<CommitmentResponse>> GetAllAsync(string userId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct);
    Task<CommitmentResponse> CreateAsync(string userId, CreateCommitmentRequest request, CancellationToken ct);
    Task<CommitmentResponse?> UpdateAsync(string userId, Guid id, UpdateCommitmentRequest request, CancellationToken ct);
}

public sealed class CommitmentService(AppDbContext db) : ICommitmentService
{
    public async Task<IReadOnlyList<CommitmentResponse>> GetAllAsync(string userId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var query = db.Commitments.AsNoTracking().Where(item => item.UserId == userId);
        if (from.HasValue) query = query.Where(item => item.EndTime >= from.Value);
        if (to.HasValue) query = query.Where(item => item.StartTime <= to.Value);
        var commitments = await query.OrderBy(item => item.StartTime).ToListAsync(ct);
        return commitments.Select(ToResponse).ToList();
    }

    public async Task<CommitmentResponse> CreateAsync(string userId, CreateCommitmentRequest request, CancellationToken ct)
    {
        Validate(request.Title, request.StartTime, request.EndTime);
        if (!await db.Users.AnyAsync(user => user.Id == userId, ct)) db.Users.Add(new UserProfile { Id = userId });
        var commitment = new Commitment
        {
            UserId = userId, Title = request.Title.Trim(), Description = request.Description,
            StartTime = request.StartTime, EndTime = request.EndTime, Location = request.Location, IsFlexible = request.IsFlexible
        };
        db.Commitments.Add(commitment);
        await db.SaveChangesAsync(ct);
        return ToResponse(commitment);
    }

    public async Task<CommitmentResponse?> UpdateAsync(string userId, Guid id, UpdateCommitmentRequest request, CancellationToken ct)
    {
        var item = await db.Commitments.SingleOrDefaultAsync(value => value.Id == id && value.UserId == userId, ct);
        if (item is null) return null;
        var start = request.StartTime ?? item.StartTime;
        var end = request.EndTime ?? item.EndTime;
        Validate(request.Title ?? item.Title, start, end);
        item.Title = request.Title?.Trim() ?? item.Title; item.Description = request.Description ?? item.Description;
        item.StartTime = start; item.EndTime = end; item.Location = request.Location ?? item.Location;
        item.IsFlexible = request.IsFlexible ?? item.IsFlexible; item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToResponse(item);
    }

    private static CommitmentResponse ToResponse(Commitment item) => new(item.Id, item.Title, item.Description, item.StartTime, item.EndTime, item.Location, item.IsFlexible);
    private static void Validate(string title, DateTimeOffset start, DateTimeOffset end) { if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Commitment title is required."); if (end <= start) throw new ArgumentException("Commitment end time must be after its start time."); }
}
