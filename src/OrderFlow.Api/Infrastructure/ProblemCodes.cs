namespace OrderFlow.Api.Infrastructure;

/// <summary>
/// Stable codes for problem details that do not come from a domain <c>Error</c>: failures mapped
/// from exceptions and responses produced by the framework itself (401, 403, 404, 429...).
/// Together with the domain codes, this means every error response carries a <c>code</c>.
/// </summary>
internal static class ProblemCodes
{
    /// <summary>The request lost an optimistic concurrency race even after retries. Safe to retry.</summary>
    public const string ConcurrencyConflict = "General.ConcurrencyConflict";

    public const string DuplicateResource = "General.DuplicateResource";
    public const string BadRequest = "General.BadRequest";
    public const string Unauthorized = "General.Unauthorized";
    public const string Forbidden = "General.Forbidden";
    public const string NotFound = "General.NotFound";
    public const string TooManyRequests = "General.TooManyRequests";
    public const string ServerError = "General.ServerError";

    public static string ForStatus(int? statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => BadRequest,
        StatusCodes.Status401Unauthorized => Unauthorized,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status429TooManyRequests => TooManyRequests,
        >= StatusCodes.Status500InternalServerError => ServerError,
        _ => $"General.Http{statusCode}",
    };
}
