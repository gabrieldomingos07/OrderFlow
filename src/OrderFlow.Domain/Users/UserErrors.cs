using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Users;

public static class UserErrors
{
    public static readonly Error EmailAlreadyInUse =
        Error.Conflict("Users.EmailAlreadyInUse", "The e-mail address is already registered.");

    // Deliberately vague: never reveal whether the e-mail or the password was wrong.
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Users.InvalidCredentials", "The e-mail or password is incorrect.");

    public static readonly Error NotAuthenticated =
        Error.Unauthorized("Users.NotAuthenticated", "The request requires an authenticated user.");

    public static readonly Error Forbidden =
        Error.Forbidden("Users.Forbidden", "The current user is not allowed to perform this action.");

    public static Error NotFound(Guid userId) =>
        Error.NotFound("Users.NotFound", $"User '{userId}' was not found.");
}
