using System.Text;
using System.Text.RegularExpressions;
using Fugue.Application.Features.Claims;
using Fugue.Application.Features.Letters;
using Fugue.Domain.Claims;
using Fugue.Domain.Ids;

namespace Fugue.Tests.Letters;

/// <summary>The sample letter (item 11b): which letter a claim gets, what it says, what a claimant's copy leaves out, and that the PDF is well formed.</summary>
public sealed class ClaimLetterTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 2, 30, 0, TimeSpan.Zero);
    private const string Correlation = "0193b4a2c1d07a3e9f";

    private static ClaimDto Claim(
        ClaimStatus status, long? approved = 450_000, string? claimantName = "Ada Tan", Market market = Market.SG, string reason = "Covered under section 4.") =>
        new(
            ClaimId.New(), "CLM-SG-2026-000123", market, Markets.CurrencyOf(market), LineOfBusiness.Motor, status, "POL-7Q2X", new DateOnly(2026, 11, 20),
            "Rear-ended at the lights on Orchard Road.", "Orchard Road", new DateTimeOffset(2026, 11, 21, 1, 0, 0, TimeSpan.Zero),
            status == ClaimStatus.Rejected ? null : approved, reason, new DateTimeOffset(2026, 11, 26, 3, 0, 0, TimeSpan.Zero),
            status == ClaimStatus.Settled ? new DateTimeOffset(2026, 11, 27, 4, 0, 0, TimeSpan.Zero) : null, Now, 7, null,
            ClaimantName: claimantName, AssignedOfficerName: claimantName is null ? null : "Sam Officer");

    /// <summary>Submit (by the claimant), pickup and decision (by staff) as the repository returns them to staff, or to the claimant (<paramref name="asClaimant"/>).</summary>
    private static ClaimTimelineEntryDto[] Timeline(ClaimStatus decided, bool asClaimant = false) =>
    [
        new(ClaimTimelineEntryId.New(), Now.AddDays(-7), null, ClaimStatus.Submitted, "Ada Tan", "Claim submitted."),
        new(ClaimTimelineEntryId.New(), Now.AddDays(-6), ClaimStatus.Submitted, ClaimStatus.UnderReview, asClaimant ? "Claims team" : "Sam Officer", "Started."),
        new(ClaimTimelineEntryId.New(), Now.AddDays(-2), ClaimStatus.UnderReview, decided == ClaimStatus.Settled ? ClaimStatus.Approved : decided,
            asClaimant ? "Claims team" : "Mia Manager", "Decided."),
    ];

    private static string Text(ClaimLetter letter) => PdfText.Extract(ClaimLetterPdf.Render(letter));

    [Theory]
    [InlineData(ClaimStatus.Approved, ClaimLetterKind.Settlement, "CLM-SG-2026-000123-settlement-letter.pdf")]
    [InlineData(ClaimStatus.Settled, ClaimLetterKind.Settlement, "CLM-SG-2026-000123-settlement-letter.pdf")]
    [InlineData(ClaimStatus.Rejected, ClaimLetterKind.Decision, "CLM-SG-2026-000123-decision-letter.pdf")]
    public void A_decided_claim_gets_the_letter_of_its_outcome(ClaimStatus status, ClaimLetterKind kind, string fileName)
    {
        ClaimLetter letter = ClaimLetter.From(Claim(status), Timeline(status), Correlation, Now);

        Assert.Equal(kind, letter.Kind);
        Assert.Equal(fileName, letter.FileName);
    }

    [Theory]
    [InlineData(ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.InfoRequested)]
    public void An_undecided_claim_has_no_letter(ClaimStatus status)
    {
        Assert.False(ClaimLetter.IsDecided(status));
        Assert.Throws<ArgumentException>(() => ClaimLetter.From(Claim(status), Timeline(ClaimStatus.Approved), Correlation, Now));
    }

    [Fact]
    public void The_settlement_letter_names_the_claimant_policy_incident_amount_reason_officer_dates_number_and_correlation_id()
    {
        string text = Text(ClaimLetter.From(Claim(ClaimStatus.Settled), Timeline(ClaimStatus.Settled), Correlation, Now));

        Assert.Contains("Settlement letter", text, StringComparison.Ordinal);
        Assert.Contains("CLM-SG-2026-000123", text, StringComparison.Ordinal);
        Assert.Contains("Ada Tan", text, StringComparison.Ordinal);
        Assert.Contains("POL-7Q2X", text, StringComparison.Ordinal);
        Assert.Contains("20 November 2026", text, StringComparison.Ordinal); // incident date
        Assert.Contains("Rear-ended at the lights on Orchard Road.", text, StringComparison.Ordinal);
        Assert.Contains("SGD 4,500.00", text, StringComparison.Ordinal);
        Assert.Contains("Covered under section 4.", text, StringComparison.Ordinal);
        Assert.Contains("Mia Manager", text, StringComparison.Ordinal); // who decided, from the timeline, not the assigned officer
        Assert.Contains("26 November 2026", text, StringComparison.Ordinal); // decided (SG local)
        Assert.Contains("27 November 2026", text, StringComparison.Ordinal); // settled
        Assert.Contains("28 November 2026", text, StringComparison.Ordinal); // letter date
        Assert.Contains(Correlation, text, StringComparison.Ordinal);
        Assert.Contains("SAMPLE DOCUMENT, NOT A LEGAL LETTER", text, StringComparison.Ordinal);
        Assert.Contains(ClaimLetter.SampleLabel, text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_decision_letter_for_a_rejection_gives_the_reason_and_no_amount()
    {
        string text = Text(ClaimLetter.From(
            Claim(ClaimStatus.Rejected, reason: "The policy had lapsed on the incident date."), Timeline(ClaimStatus.Rejected), Correlation, Now));

        Assert.Contains("Decision on your claim", text, StringComparison.Ordinal);
        Assert.Contains("Rejected", text, StringComparison.Ordinal);
        Assert.Contains("The policy had lapsed on the incident date.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Approved amount", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SGD ", text, StringComparison.Ordinal);
        Assert.Contains(ClaimLetter.SampleLabel, text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_claimants_copy_reads_staff_as_the_claims_team_and_takes_the_name_from_their_own_submit_row()
    {
        // As the claimant reads it: the staff-only fields are null and staff actors already read "Claims team".
        string text = Text(ClaimLetter.From(
            Claim(ClaimStatus.Approved, claimantName: null), Timeline(ClaimStatus.Approved, asClaimant: true), Correlation, Now));

        Assert.Contains("Ada Tan", text, StringComparison.Ordinal);
        Assert.Contains("Claims team", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Mia Manager", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Sam Officer", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(450_000, "SGD", "SGD 4,500.00")]
    [InlineData(1_500_000_000, "IDR", "IDR 15,000,000.00")]
    [InlineData(5, "THB", "THB 0.05")]
    [InlineData(100, "HKD", "HKD 1.00")]
    public void Money_is_formatted_from_minor_units_with_two_decimals(long minor, string currency, string expected) =>
        Assert.Equal(expected, ClaimLetter.Money(minor, currency));

    [Fact]
    public void The_pdf_is_well_formed_one_page_with_a_byte_exact_xref()
    {
        byte[] pdf = ClaimLetterPdf.Render(ClaimLetter.From(Claim(ClaimStatus.Approved), Timeline(ClaimStatus.Approved), Correlation, Now));
        string raw = Encoding.Latin1.GetString(pdf);

        Assert.StartsWith("%PDF-1.4\n", raw, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", raw, StringComparison.Ordinal);
        Assert.Contains("/Count 1", raw, StringComparison.Ordinal);

        int startxref = int.Parse(Regex.Match(raw, @"startxref\n(\d+)\n").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.StartsWith("xref\n0 9\n", raw[startxref..], StringComparison.Ordinal);
        MatchCollection entries = Regex.Matches(raw[startxref..], @"(\d{10}) 00000 n ");
        Assert.Equal(8, entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            int offset = int.Parse(entries[i].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.StartsWith($"{i + 1} 0 obj\n", raw[offset..], StringComparison.Ordinal);
        }

        int length = int.Parse(Regex.Match(raw, @"/Length (\d+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        int streamStart = raw.IndexOf("stream\n", StringComparison.Ordinal) + "stream\n".Length;
        Assert.Equal("\nendstream", raw.Substring(streamStart + length, "\nendstream".Length));
    }

    [Fact]
    public void Text_is_escaped_and_characters_outside_latin1_print_as_a_question_mark()
    {
        string text = Text(ClaimLetter.From(
            Claim(ClaimStatus.Rejected, reason: @"Excluded (see clause 2\b) for ソ damage – sorry."), Timeline(ClaimStatus.Rejected), Correlation, Now));

        Assert.Contains(@"Excluded (see clause 2\b) for ? damage - sorry.", text, StringComparison.Ordinal);
    }
}
