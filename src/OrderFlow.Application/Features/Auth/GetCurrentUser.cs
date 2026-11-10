using OrderFlow.Application.Abstractions.Authentication;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Users;

namespace OrderFlow.Application.Features.Auth;

/// <summary>Returns the profile of the authenticated user.</summary>
public sealed record GetCurrentUserQuery : IQuery<UserResponse>;

internal sealed class GetCurrentUserQueryHandler(IUserRepository users, ICurrentUser currentUser)
    : IQueryHandler<GetCurrentUserQuery, UserResponse>
{
    public async Task<Result<UserResponse>> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return UserErrors.NotAuthenticated;
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user is null ? UserErrors.NotFound(userId) : UserResponse.FromDomain(user);
    }
}
