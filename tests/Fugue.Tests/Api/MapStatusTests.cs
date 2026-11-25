using Fugue.Domain.Results;

namespace Fugue.Tests.Api;

/// <summary>Pins D-009: the ErrorType to HTTP status mapping in <c>ApiControllerBase.MapStatus</c>.</summary>
public sealed class MapStatusTests
{
    [Theory]
    [InlineData(ErrorType.Failure, 500)]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.PreconditionFailed, 412)]
    [InlineData(ErrorType.PreconditionRequired, 428)]
    [InlineData(ErrorType.Unavailable, 503)]
    public void MapStatus_returns_the_expected_status(ErrorType type, int expected) =>
        Assert.Equal(expected, ProbeController.ExposeMapStatus(type));

    [Fact]
    public void Every_error_type_has_an_expectation()
    {
        int covered = typeof(MapStatusTests).GetMethod(nameof(MapStatus_returns_the_expected_status))!
            .GetCustomAttributes(typeof(InlineDataAttribute), inherit: false).Length;

        Assert.Equal(Enum.GetValues<ErrorType>().Length, covered);
    }
}
