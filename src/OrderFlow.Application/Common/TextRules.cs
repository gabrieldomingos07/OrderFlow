using FluentValidation;

namespace OrderFlow.Application.Common;

internal static class TextRules
{
    /// <summary>
    /// PostgreSQL text columns cannot store U+0000, so such input would fail inside the database
    /// with a 500. Rejecting it up front turns that into a regular validation error.
    /// </summary>
    public static IRuleBuilderOptions<T, TText> NoNullCharacters<T, TText>(this IRuleBuilder<T, TText> rule)
        where TText : class? =>
        rule
            .Must(value => value is not string text || !text.Contains('\0', StringComparison.Ordinal))
            .WithMessage("'{PropertyName}' cannot contain null characters.");
}
