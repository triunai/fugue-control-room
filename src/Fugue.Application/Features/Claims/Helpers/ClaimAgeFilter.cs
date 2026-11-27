using FluentValidation;

namespace Fugue.Application.Features.Claims.Helpers;

/// <summary>
/// The age filters shared by the claim list and counts. Age is the whole number of elapsed days since submission (UTC instants, no market calendar),
/// so <c>minAgeDays=3</c> means submitted at least 3 x 24h ago and <c>maxAgeDays=7</c> means younger than 8 x 24h. Both bounds are inclusive.
/// </summary>
internal static class ClaimAgeFilter
{
    public const int MaxDays = 3650;

    public static void Rules<T>(AbstractValidator<T> validator, System.Linq.Expressions.Expression<Func<T, int?>> min, System.Linq.Expressions.Expression<Func<T, int?>> max)
    {
        Func<T, int?> minOf = min.Compile();
        Func<T, int?> maxOf = max.Compile();
        validator.RuleFor(min).InclusiveBetween(0, MaxDays).When(x => minOf(x) is not null).WithMessage($"MinAgeDays must be between 0 and {MaxDays}.");
        validator.RuleFor(max).InclusiveBetween(0, MaxDays).When(x => maxOf(x) is not null).WithMessage($"MaxAgeDays must be between 0 and {MaxDays}.");
        validator.RuleFor(max).Must((x, _) => maxOf(x)!.Value >= minOf(x)!.Value)
            .When(x => minOf(x) is not null && maxOf(x) is not null)
            .WithMessage("MaxAgeDays must not be less than MinAgeDays.");
    }

    /// <summary>Submitted at or before this instant: at least <paramref name="minAgeDays"/> days old.</summary>
    public static DateTimeOffset? OlderThanOrAt(DateTimeOffset now, int? minAgeDays) => minAgeDays is { } d ? now.AddDays(-d) : null;

    /// <summary>Submitted strictly after this instant: at most <paramref name="maxAgeDays"/> whole days old.</summary>
    public static DateTimeOffset? NewerThan(DateTimeOffset now, int? maxAgeDays) => maxAgeDays is { } d ? now.AddDays(-(d + 1)) : null;
}
