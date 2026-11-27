using FluentResults;
using Fugue.Domain.Claims;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Tests.Domain;

/// <summary>
/// What the claimant reads (timeline notes) and what the API shows on a refusal (error messages and field names). Found by mutation testing
/// (docs/quality/mutation): the state machine was well covered, the words it writes were not.
/// </summary>
public sealed class ClaimNarrativeTests
{
    private static readonly DateTimeOffset T0 = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly UserId Claimant = UserId.New();
    private static readonly UserId Officer = UserId.New();

    private static Claim Submitted(string? location = "Orchard Rd") => Claim.Submit(
        ClaimNumber.Format(Market.SG, 2030, 1), Market.SG, LineOfBusiness.Motor, Claimant, "Ada Claimant", "POL-1", new DateOnly(2030, 1, 1),
        "Rear-ended.", location, T0);

    private static Claim UnderReview()
    {
        Claim claim = Submitted();
        Assert.True(claim.Pickup(Officer, "Officer", T0).IsSuccess);
        return claim;
    }

    private static string LastNote(Claim claim) => claim.Timeline.Last().Note;

    private static FugueError Error(Result result) => Assert.IsType<FugueError>(Assert.Single(result.Errors));

    [Fact]
    public void Each_move_writes_the_note_the_claimant_reads()
    {
        Claim claim = Submitted();
        Assert.Equal("Claim submitted.", LastNote(claim));

        Assert.True(claim.Pickup(Officer, "Officer", T0).IsSuccess);
        Assert.Equal("A claims officer has started reviewing your claim.", LastNote(claim));

        Assert.True(claim.RequestInfo("  Photos?  ", Officer, "Officer", T0).IsSuccess);
        Assert.Equal("More information needed: Photos?", LastNote(claim));

        Assert.True(claim.ProvideInfo(claim.OpenInfoRequest!.Id, "  Attached.  ", Claimant, "Ada Claimant", T0).IsSuccess);
        Assert.Equal("Information provided: Attached.", LastNote(claim));

        Assert.True(claim.SetReserve(1000).IsSuccess);
        Assert.True(claim.Approve(900, "  Covered.  ", Officer, "Officer", T0).IsSuccess);
        Assert.Equal("Your claim has been approved. Covered.", LastNote(claim));

        Assert.True(claim.Settle(Officer, "Officer", T0).IsSuccess);
        Assert.Equal("Your claim has been settled.", LastNote(claim));
    }

    [Fact]
    public void A_rejection_note_carries_the_reason()
    {
        Claim claim = UnderReview();

        Assert.True(claim.Reject("  Not covered.  ", Officer, "Officer", T0).IsSuccess);

        Assert.Equal("Your claim has been rejected. Not covered.", LastNote(claim));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_location_is_stored_as_null(string? location) => Assert.Null(Submitted(location).IncidentLocation);

    [Fact]
    public void A_location_is_trimmed() => Assert.Equal("Orchard Rd", Submitted("  Orchard Rd  ").IncidentLocation);

    [Fact]
    public void A_zero_reserve_is_refused_with_its_message_and_field()
    {
        FugueError error = Error(UnderReview().SetReserve(0));

        Assert.Equal("claim.amount_invalid", error.Code);
        Assert.Equal("The reserve must be greater than zero.", error.Message);
        Assert.Equal(["The reserve must be greater than zero."], error.Fields!["amountMinor"]);
    }

    [Fact]
    public void Approving_without_a_reserve_says_a_reserve_must_be_set_first()
    {
        FugueError error = Error(UnderReview().Approve(100, "Ok.", Officer, "Officer", T0));

        Assert.Equal("claim.reserve_required", error.Code);
        Assert.Equal("A reserve must be set before a claim can be approved.", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void An_approved_amount_outside_zero_to_reserve_is_refused_with_its_message(long amount)
    {
        Claim claim = UnderReview();
        Assert.True(claim.SetReserve(1000).IsSuccess);

        FugueError error = Error(claim.Approve(amount, "Ok.", Officer, "Officer", T0));

        Assert.Equal("claim.amount_invalid", error.Code);
        Assert.Equal("The approved amount must be greater than zero and not more than the reserve.", error.Message);
        Assert.Equal([error.Message], error.Fields!["amountMinor"]);
    }

    [Fact]
    public void Answering_with_a_wrong_request_id_is_a_404_that_says_so()
    {
        Claim claim = UnderReview();
        Assert.True(claim.RequestInfo("Photos?", Officer, "Officer", T0).IsSuccess);

        FugueError error = Error(claim.ProvideInfo(ClaimInfoRequestId.New(), "Here.", Claimant, "Ada Claimant", T0));

        Assert.Equal("claim.info_request_not_found", error.Code);
        Assert.Equal("There is no open information request with that id on this claim.", error.Message);
    }

    [Fact]
    public void Stamp_columns_start_empty_until_the_persistence_layer_fills_them()
    {
        Claim claim = Submitted();

        Assert.Equal(string.Empty, claim.CreatedBy);
        Assert.Equal(string.Empty, claim.UpdatedBy);
    }
}
