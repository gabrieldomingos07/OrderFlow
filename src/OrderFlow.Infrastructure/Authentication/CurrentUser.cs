using Microsoft.AspNetCore.Http;
using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Domain.Users;

namespace OrderFlow.Infrastructure.Authentication;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(ClaimNames.Subject)?.Value, out var id)
            ? id
            : null;

    public bool IsAdmin => httpContextAccessor.HttpContext?.User.IsInRole(nameof(UserRole.Admin)) ?? false;
}
