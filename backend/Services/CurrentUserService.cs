using System.Security.Claims;

namespace backend.Services;

public interface ICurrentUserService
{
    string GetRequiredUserId(ClaimsPrincipal principal);
}

public sealed class CurrentUserService : ICurrentUserService
{
    public string GetRequiredUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("The authenticated user has no subject identifier.");
}
