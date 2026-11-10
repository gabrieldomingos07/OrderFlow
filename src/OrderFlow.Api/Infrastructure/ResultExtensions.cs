using OrderFlow.Domain.Common;

namespace OrderFlow.Api.Infrastructure;

/// <summary>Maps application results to HTTP responses.</summary>
internal static class ResultExtensions
{
    public static IResult Match<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();

    public static IResult Match(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : result.Error.ToProblem();

    public static IResult ToProblem(this Error error)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        if (error is ValidationError validation)
        {
            var errors = validation.Errors.ToDictionary(
                pair => ToCamelCasePath(pair.Key),
                pair => pair.Value,
                StringComparer.Ordinal);

            return Results.ValidationProblem(errors, title: validation.Description, extensions: extensions);
        }

        return Results.Problem(
            statusCode: ToStatusCode(error.Type),
            title: ToTitle(error.Type),
            detail: error.Description,
            extensions: extensions);
    }

    private static int ToStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static string ToTitle(ErrorType type) => type switch
    {
        ErrorType.Validation => "Bad request",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        ErrorType.NotFound => "Not found",
        ErrorType.Conflict => "Conflict",
        _ => "Server error",
    };

    /// <summary>"Items[0].ProductId" becomes "items[0].productId", matching the JSON payload.</summary>
    private static string ToCamelCasePath(string path) =>
        string.Join('.', path.Split('.').Select(segment =>
            segment.Length == 0 ? segment : char.ToLowerInvariant(segment[0]) + segment[1..]));
}
