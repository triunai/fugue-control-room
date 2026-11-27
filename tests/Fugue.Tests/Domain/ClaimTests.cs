using FluentResults;
using Fugue.Domain.Claims;
using Fugue.Domain.Events;
using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Tests.Domain;

/// <summary>The Claim state machine (decision C of the claims window): every legal move, every illegal move, and the money rules.</summary>
public sealed class ClaimTests
{
    private static readonly DateTimeOffset T0 = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly UserId Claimant = UserId.New();
    private static readonly UserId Officer = UserId.New();

    private static Claim NewClaim(Market market = Market.SG) => Claim.Submit(
        ClaimNumber.Format(market, 2030, 1), market, LineOfBusiness.Motor, Claimant, "Ada Claimant", " POL-1 ", new DateOnly(2030, 1, 1),
        "  Rear-ended at the lights. ", "  Orchard Rd ", T0);

    /// <summary>A claim brought to <paramref name="status"/> by legal moves; the open states carry a reserve of 1000 so approving is possible.</summary>
    private static Claim At(ClaimStatus status)
    {
        Claim claim = NewClaim();
        if (status == ClaimStatus.Submitted)
        {
            return claim;
        }

        Assert.True(claim.Pickup(Officer, "Officer", T0).IsSuccess);
        if (status == ClaimStatus.InfoRequested)
        {
            Assert.True(claim.RequestInfo("Photos of the damage?", Officer, "Officer", T0).IsSuccess);
            Assert.True(claim.SetReserve(1000).IsSuccess);
            return claim;
        }

        Assert.True(claim.SetReserve(1000).IsSuccess);
        switch (status)
        {
            case ClaimStatus.Approved:
            case ClaimStatus.Settled:
                Assert.True(claim.Approve(800, "Covered.", Officer, "Officer", T0).IsSuccess);
                if (status == ClaimStatus.Settled)
                {
                    Assert.True(claim.Settle(Officer, "Officer", T0).IsSuccess);
                }

                break;
            case ClaimStatus.Rejected:
                Assert.True(claim.Reject("Not covered.", Officer, "Officer", T0).IsSuccess);
                break;
        }

        return claim;
    }

    public enum Move
    {
        Pickup,
        RequestInfo,
        ProvideInfo,
        SetReserve,
        Approve,
        Reject,
        Settle,
    }

    private static Result Apply(Claim claim, Move move) => move switch
    {
        Move.Pickup => claim.Pickup(Officer, "Officer", T0.AddHours(1)),
        Move.RequestInfo => claim.RequestInfo("Anything else?", Officer, "Officer", T0.AddHours(1)),
        Move.ProvideInfo => claim.ProvideInfo(claim.OpenInfoRequest?.Id ?? ClaimInfoRequestId.New(), "Here you go", Claimant, "Ada Claimant", T0.AddHours(1)),
        Move.SetReserve => claim.SetReserve(2000),
        Move.Approve => claim.Approve(500, "Covered.", Officer, "Officer", T0.AddHours(1)),
        Move.Reject => claim.Reject("Not covered.", Officer, "Officer", T0.AddHours(1)),
        Move.Settle => claim.Settle(Officer, "Officer", T0.AddHours(1)),
        _ => throw new ArgumentOutOfRangeException(nameof(move)),
    };

    /// <summary>The whole machine: from each status, the one-or-two moves that are legal and where they lead (null target = status unchanged).</summary>
    private static readonly Dictionary<(ClaimStatus From, Move Move), ClaimStatus?> Legal = new()
    {
        [(ClaimStatus.Submitted, Move.Pickup)] = ClaimStatus.UnderReview,
        [(ClaimStatus.UnderReview, Move.RequestInfo)] = ClaimStatus.InfoRequested,
        [(ClaimStatus.UnderReview, Move.SetReserve)] = null,
        [(ClaimStatus.UnderReview, Move.Approve)] = ClaimStatus.Approved,
        [(ClaimStatus.UnderReview, Move.Reject)] = ClaimStatus.Rejected,
        [(ClaimStatus.InfoRequested, Move.ProvideInfo)] = ClaimStatus.UnderReview,
        [(ClaimStatus.InfoRequested, Move.SetReserve)] = null,
        [(ClaimStatus.Approved, Move.Settle)] = ClaimStatus.Settled,
    };

    public static TheoryData<ClaimStatus, Move> Table()
    {
        var data = new TheoryData<ClaimStatus, Move>();
        foreach (ClaimStatus status in Enum.GetValues<ClaimStatus>())
        {
            foreach (Move move in Enum.GetValues<Move>())
            {
                data.Add(status, move);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Table))]
    public void The_transition_table_holds_for_every_status_and_move(ClaimStatus from, Move move)
    {
        Claim claim = At(from);
        int timeline = claim.Timeline.Count;
        int events = ((IHasDomainEvents)claim).DomainEvents.Count;

        Result result = Apply(claim, move);

        if (Legal.TryGetValue((from, move), out ClaimStatus? target))
        {
            Assert.True(result.IsSuccess, $"{move} from {from} should be legal");
            Assert.Equal(target ?? from, claim.Status);
            // A status change writes exactly one timeline row and one event; the reserve is internal: an event (claim.reserve.set.v1), no timeline row.
            int expected = target is null ? 0 : 1;
            Assert.Equal(timeline + expected, claim.Timeline.Count);
            Assert.Equal(events + 1, ((IHasDomainEvents)claim).DomainEvents.Count);
            if (target is not null)
            {
                ClaimTimelineEntry last = claim.Timeline.Last();
                Assert.Equal(from, last.FromStatus);
                Assert.Equal(target, last.ToStatus);
            }
        }
        else
        {
            Assert.True(result.IsFailed, $"{move} from {from} should be illegal");
            FugueError error = Assert.IsType<FugueError>(result.Errors[0]);
            Assert.Equal(ErrorType.Conflict, error.Type);
            Assert.Equal("claim.invalid_transition", error.Code);
            Assert.Contains(from.ToString(), error.Message, StringComparison.Ordinal);
            Assert.Equal(from, claim.Status);
            Assert.Equal(timeline, claim.Timeline.Count);
            Assert.Equal(events, ((IHasDomainEvents)claim).DomainEvents.Count);
        }
    }

    [Fact]
    public void An_illegal_move_message_names_the_states_and_carries_no_user_data()
    {
        Claim claim = At(ClaimStatus.Rejected);

        FugueError error = Assert.IsType<FugueError>(claim.Approve(1, "reason-with-secret", Officer, "Officer", T0).Errors[0]);

        Assert.Equal("A claim cannot move from Rejected to Approved.", error.Message);
        Assert.DoesNotContain("secret", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(claim.PolicyNumber, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClaimStatus.Rejected)]
    [InlineData(ClaimStatus.Settled)]
    public void Terminal_states_accept_no_move_at_all(ClaimStatus terminal)
    {
        foreach (Move move in Enum.GetValues<Move>())
        {
            Claim claim = At(terminal);
            Assert.True(Apply(claim, move).IsFailed, $"{move} must fail from {terminal}");
            Assert.Equal(terminal, claim.Status);
        }
    }

    [Fact]
    public void Submit_normalises_input_fixes_the_currency_by_market_and_records_the_first_timeline_row()
    {
        Claim claim = NewClaim(Market.ID);

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal("IDR", claim.Currency);
        Assert.Equal("POL-1", claim.PolicyNumber);
        Assert.Equal("Rear-ended at the lights.", claim.IncidentDescription);
        Assert.Equal("Orchard Rd", claim.IncidentLocation);
        Assert.Equal(T0, claim.SubmittedAt);
        Assert.Null(claim.ReserveMinor);
        ClaimTimelineEntry first = Assert.Single(claim.Timeline);
        Assert.Null(first.FromStatus);
        Assert.Equal(ClaimStatus.Submitted, first.ToStatus);
        Assert.Equal(Claimant, first.ActorUserId);
        IDomainEvent evt = Assert.Single(((IHasDomainEvents)claim).DomainEvents);
        Assert.Equal("claim.submitted.v1", evt.EventType);
    }

    [Theory]
    [InlineData(Market.SG, "SGD")]
    [InlineData(Market.MY, "MYR")]
    [InlineData(Market.TH, "THB")]
    [InlineData(Market.HK, "HKD")]
    [InlineData(Market.ID, "IDR")]
    [InlineData(Market.PH, "PHP")]
    public void Each_market_has_exactly_its_own_currency(Market market, string currency) =>
        Assert.Equal(currency, NewClaim(market).Currency);

    [Fact]
    public void Claim_numbers_carry_market_year_and_a_padded_sequence() =>
        Assert.Equal("CLM-TH-2030-000042", ClaimNumber.Format(Market.TH, 2030, 42));

    [Fact]
    public void Pickup_assigns_the_officer_and_the_event_has_a_thin_payload()
    {
        Claim claim = NewClaim();

        claim.Pickup(Officer, "Officer", T0.AddMinutes(5));

        Assert.Equal(Officer, claim.AssignedOfficerId);
        Assert.Equal(T0.AddMinutes(5), claim.AssignedAt);
        Assert.Equal(["claim.submitted.v1", "claim.assigned.v1"], ((IHasDomainEvents)claim).DomainEvents.Select(e => e.EventType));
    }

    [Fact]
    public void Requesting_info_opens_exactly_one_question_and_a_second_request_is_refused()
    {
        Claim claim = At(ClaimStatus.UnderReview);

        Assert.True(claim.RequestInfo("  Photos?  ", Officer, "Officer", T0).IsSuccess);

        ClaimInfoRequest open = Assert.Single(claim.InfoRequests);
        Assert.True(open.IsOpen);
        Assert.Equal("Photos?", open.Question);
        Assert.Equal(open.Id, claim.OpenInfoRequest!.Id);
        Assert.True(claim.RequestInfo("Another?", Officer, "Officer", T0).IsFailed);
        Assert.Single(claim.InfoRequests);
    }

    [Fact]
    public void Answering_closes_the_request_and_returns_the_claim_to_review()
    {
        Claim claim = At(ClaimStatus.InfoRequested);
        ClaimInfoRequestId id = claim.OpenInfoRequest!.Id;

        Result result = claim.ProvideInfo(id, "  Attached.  ", Claimant, "Ada Claimant", T0.AddHours(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Null(claim.OpenInfoRequest);
        ClaimInfoRequest answered = Assert.Single(claim.InfoRequests);
        Assert.Equal("Attached.", answered.Response);
        Assert.Equal(T0.AddHours(2), answered.RespondedAt);
    }

    [Fact]
    public void Answering_with_an_id_that_is_not_the_open_request_is_a_404_and_changes_nothing()
    {
        Claim claim = At(ClaimStatus.InfoRequested);

        Result result = claim.ProvideInfo(ClaimInfoRequestId.New(), "x", Claimant, "Ada Claimant", T0);

        FugueError error = Assert.IsType<FugueError>(result.Errors[0]);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("claim.info_request_not_found", error.Code);
        Assert.Equal(ClaimStatus.InfoRequested, claim.Status);
        Assert.NotNull(claim.OpenInfoRequest);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    public void A_reserve_must_be_positive(long amount)
    {
        Claim claim = At(ClaimStatus.UnderReview);

        FugueError error = Assert.IsType<FugueError>(claim.SetReserve(amount).Errors[0]);

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("claim.amount_invalid", error.Code);
        Assert.Equal(1000, claim.ReserveMinor); // the earlier reserve stands
    }

    [Fact]
    public void Setting_the_reserve_raises_a_thin_event_without_the_amount()
    {
        Claim claim = At(ClaimStatus.UnderReview);
        int before = ((IHasDomainEvents)claim).DomainEvents.Count;

        Assert.True(claim.SetReserve(2500).IsSuccess);

        IDomainEvent evt = ((IHasDomainEvents)claim).DomainEvents.Last();
        Assert.Equal(before + 1, ((IHasDomainEvents)claim).DomainEvents.Count);
        Assert.Equal("claim.reserve.set.v1", evt.EventType);
        Assert.Equal(claim.Id.ToString(), evt.AggregateId);
        Assert.DoesNotContain(evt.GetType().GetProperties(), p => p.Name.Contains("Reserve", StringComparison.Ordinal) || p.Name.Contains("Amount", StringComparison.Ordinal));
    }

    [Fact]
    public void A_refused_reserve_raises_no_event()
    {
        Claim claim = At(ClaimStatus.UnderReview);
        int before = ((IHasDomainEvents)claim).DomainEvents.Count;

        Assert.True(claim.SetReserve(0).IsFailed);

        Assert.Equal(before, ((IHasDomainEvents)claim).DomainEvents.Count);
    }

    [Fact]
    public void The_reserve_can_be_revised_while_the_claim_is_open()
    {
        Claim claim = At(ClaimStatus.UnderReview);

        Assert.True(claim.SetReserve(2500).IsSuccess);

        Assert.Equal(2500, claim.ReserveMinor);
    }

    [Fact]
    public void Approving_without_a_reserve_is_a_409_reserve_required()
    {
        Claim claim = NewClaim();
        claim.Pickup(Officer, "Officer", T0);

        FugueError error = Assert.IsType<FugueError>(claim.Approve(100, "ok", Officer, "Officer", T0).Errors[0]);

        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal("claim.reserve_required", error.Code);
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1001L)] // the reserve is 1000
    public void An_approved_amount_outside_zero_to_reserve_is_a_400_and_changes_nothing(long amount)
    {
        Claim claim = At(ClaimStatus.UnderReview);

        FugueError error = Assert.IsType<FugueError>(claim.Approve(amount, "ok", Officer, "Officer", T0).Errors[0]);

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("claim.amount_invalid", error.Code);
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Null(claim.ApprovedAmountMinor);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(1000L)] // exactly the reserve
    public void An_approved_amount_up_to_and_including_the_reserve_is_accepted(long amount)
    {
        Claim claim = At(ClaimStatus.UnderReview);

        Assert.True(claim.Approve(amount, "  Covered. ", Officer, "Officer", T0.AddDays(1)).IsSuccess);

        Assert.Equal(ClaimStatus.Approved, claim.Status);
        Assert.Equal(amount, claim.ApprovedAmountMinor);
        Assert.Equal("Covered.", claim.DecisionReason);
        Assert.Equal(T0.AddDays(1), claim.DecidedAt);
    }

    [Fact]
    public void Rejecting_needs_no_reserve_and_records_the_reason()
    {
        Claim claim = NewClaim();
        claim.Pickup(Officer, "Officer", T0);

        Assert.True(claim.Reject("Policy lapsed.", Officer, "Officer", T0.AddDays(1)).IsSuccess);

        Assert.Equal(ClaimStatus.Rejected, claim.Status);
        Assert.Null(claim.ApprovedAmountMinor);
        Assert.Equal("Policy lapsed.", claim.DecisionReason);
        Assert.Equal("claim.decided.v1", ((IHasDomainEvents)claim).DomainEvents.Last().EventType);
    }

    [Fact]
    public void Settling_stamps_the_time_and_emits_the_settled_event()
    {
        Claim claim = At(ClaimStatus.Approved);

        Assert.True(claim.Settle(Officer, "Officer", T0.AddDays(3)).IsSuccess);

        Assert.Equal(T0.AddDays(3), claim.SettledAt);
        Assert.Equal("claim.settled.v1", ((IHasDomainEvents)claim).DomainEvents.Last().EventType);
    }

    [Fact]
    public void The_timeline_never_carries_the_reserve_or_the_approved_amount()
    {
        Claim claim = At(ClaimStatus.Settled);

        Assert.All(claim.Timeline, e =>
        {
            Assert.DoesNotContain("1000", e.Note, StringComparison.Ordinal);
            Assert.DoesNotContain("800", e.Note, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Event_payloads_are_thin_and_never_name_money_or_free_text()
    {
        Claim claim = At(ClaimStatus.Settled);

        string[] banned = ["Reserve", "Amount", "Description", "Reason", "Question", "Answer", "Policy"];
        Assert.All(((IHasDomainEvents)claim).DomainEvents, e =>
            Assert.DoesNotContain(e.GetType().GetProperties(), p => banned.Any(b => p.Name.Contains(b, StringComparison.Ordinal))));
    }

    [Theory]
    [InlineData(Market.SG, 8)]
    [InlineData(Market.HK, 8)]
    [InlineData(Market.MY, 8)]
    [InlineData(Market.PH, 8)]
    [InlineData(Market.TH, 7)]
    [InlineData(Market.ID, 7)]
    public void Local_today_uses_the_markets_fixed_offset(Market market, int offset)
    {
        var lateUtc = new DateTimeOffset(2030, 3, 10, 16, 30, 0, TimeSpan.Zero);

        DateOnly today = Markets.LocalToday(market, lateUtc);

        Assert.Equal(DateOnly.FromDateTime(lateUtc.UtcDateTime.AddHours(offset)), today);
        Assert.Equal(offset == 8 ? new DateOnly(2030, 3, 11) : new DateOnly(2030, 3, 10), today);
    }
}
