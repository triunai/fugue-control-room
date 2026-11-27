using FluentValidation;

namespace Fugue.Application.Features.Customers.Helpers;

/// <summary>Field rules shared by the create and update validators.</summary>
internal static class CustomerRules
{
    /// <summary>E.164: a plus, a non-zero first digit, up to 15 digits in total.</summary>
    private static readonly System.Text.RegularExpressions.Regex E164 =
        new(@"^\+[1-9]\d{1,14}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.Compiled);

    public static IRuleBuilderOptions<T, string> CustomerName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(200);

    public static IRuleBuilderOptions<T, string> CustomerEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(254).EmailAddress();

    public static IRuleBuilderOptions<T, string?> CustomerPhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(p => string.IsNullOrWhiteSpace(p) || E164.IsMatch(p.Trim()))
            .WithMessage("Phone must be in E.164 format, for example +14155550100.");
}
