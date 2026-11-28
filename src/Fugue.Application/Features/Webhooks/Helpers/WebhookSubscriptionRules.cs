using FluentValidation;
using Fugue.Domain.Webhooks;

namespace Fugue.Application.Features.Webhooks.Helpers;

/// <summary>Shared FluentValidation rules for the subscription commands; the SSRF guard (DNS, ranges) is a separate port because it needs I/O.</summary>
internal static class WebhookSubscriptionRules
{
    public static IRuleBuilderOptions<T, string> SubscriptionName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(100);

    /// <summary>Syntax only: an absolute http(s) URL without credentials or a fragment. Reachability rules are the guard's.</summary>
    public static IRuleBuilderOptions<T, string> SubscriptionUrl<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(2000)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out Uri? uri) && uri.Scheme is "https" or "http" && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0)
            .WithMessage("Url must be an absolute https URL without credentials or a fragment.");

    public static IRuleBuilderOptions<T, string[]> SubscriptionEventTypes<T>(this IRuleBuilder<T, string[]> rule) =>
        rule.NotNull().Must(types => types is { Length: > 0 and <= EventTypeFilter.MaxFilters }).WithMessage($"Give 1 to {EventTypeFilter.MaxFilters} event type filters.")
            .Must(types => types is null || types.All(EventTypeFilter.IsValid)).WithMessage("Each filter is '*', 'aggregate.*' or an exact versioned type such as 'customer.created.v1'.")
            .Must(types => types is null || types.Distinct(StringComparer.Ordinal).Count() == types.Length).WithMessage("Filters must be distinct.");
}
