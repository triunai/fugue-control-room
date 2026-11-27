using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using FluentResults;
using Fugue.Application.Features.Import.Helpers;
using Fugue.Application.Features.Import.Models;
using Fugue.Domain.Results;

namespace Fugue.Tests.Import;

/// <summary>
/// The file readers behind the Import page (item 11a), against the committed fixtures in <c>Fixtures/</c> (the .xlsx was written by exceljs, the
/// same library the console saves workbooks with) and against malformed input. Each reader either reads the file or fails with
/// <c>import.unreadable</c>; none throws.
/// </summary>
public sealed class ImportParserTests
{
    private static readonly IReadOnlyDictionary<string, string> Routing = new Dictionary<string, string>
    {
        ["sg"] = "SG",
        ["my"] = "MY",
        ["th"] = "TH",
        ["hk"] = "HK",
        ["id"] = "ID",
        ["ph"] = "PH",
        ["harbourbrokers.example"] = "HK",
    };

    private static readonly DateOnly Today = new(2026, 11, 28);

    private static byte[] Fixture(string name, [CallerFilePath] string here = "") =>
        File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", name));

    private static void AssertUnreadable<T>(Result<T> result)
    {
        Assert.True(result.IsFailed);
        FugueError error = Assert.IsType<FugueError>(result.Errors[0]);
        Assert.Equal("import.unreadable", error.Code);
        Assert.Equal(ErrorType.Validation, error.Type);
    }

    [Fact]
    public void Csv_reads_the_header_quoted_cells_with_commas_and_doubled_quotes()
    {
        ImportSheet sheet = SpreadsheetReader.Read("claims.csv", Fixture("claims.csv")).Value;

        Assert.Equal(["Claimant email", "Market", "Line", "Policy no.", "Date of incident", "Where", "What happened"], sheet.Headers);
        Assert.Equal(4, sheet.Rows.Count);
        Assert.Equal(2, sheet.Rows[0].Number);
        Assert.Equal("Orchard Road, outside Ngee Ann City", sheet.Rows[0].Cells[5]);
        Assert.Equal("Side mirror knocked off by a passing scooter, \"no plate seen\".", sheet.Rows[2].Cells[6]);
        Assert.Equal(string.Empty, sheet.Rows[2].Cells[3]);
        Assert.All(sheet.Rows, r => Assert.Matches("^[0-9a-f]{64}$", r.Key));
        Assert.Equal(4, sheet.Rows.Select(r => r.Key).Distinct().Count());
    }

    [Fact]
    public void Csv_with_semicolons_a_bom_and_crlf_is_read_and_the_same_row_gives_the_same_key_in_another_file()
    {
        byte[] semicolon = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("Policy;Market\r\nPOL-1;SG\r\n\r\nPOL-2;MY\r\n")];
        ImportSheet a = SpreadsheetReader.ReadCsv(semicolon).Value;
        ImportSheet b = SpreadsheetReader.ReadCsv(Encoding.UTF8.GetBytes("Policy,Market\nPOL-2,MY\n")).Value;

        Assert.Equal(["Policy", "Market"], a.Headers);
        Assert.Equal(2, a.Rows.Count); // the blank line is skipped
        Assert.Equal(4, a.Rows[1].Number); // the spreadsheet row number, counting the blank line
        Assert.Equal(a.Rows[1].Key, b.Rows[0].Key);
    }

    [Theory]
    [InlineData("Policy,Market\nPOL-1,\"SG\n")] // a quote that never closes
    [InlineData("")]
    [InlineData("\n\n  \n")]
    [InlineData("Policy,Market\n")] // header only
    public void Malformed_csv_is_unreadable(string text) =>
        AssertUnreadable(SpreadsheetReader.ReadCsv(Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void Binary_bytes_are_not_a_csv() =>
        AssertUnreadable(SpreadsheetReader.Read("claims.csv", [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00]));

    [Fact]
    public void Xlsx_reads_shared_and_rich_strings_dates_by_style_numbers_and_cached_formula_values()
    {
        ImportSheet sheet = SpreadsheetReader.Read("claims.xlsx", Fixture("claims.xlsx")).Value;

        Assert.Equal("Date of incident", sheet.Headers[4]);
        Assert.Equal([2, 3, 5, 6], sheet.Rows.Select(r => r.Number)); // row 4 is empty in the workbook
        Assert.Equal("2026-11-21", sheet.Rows[0].Cells[4]); // dd/mm/yyyy custom format
        Assert.Equal("2026-11-10", sheet.Rows[3].Cells[4]); // d mmm yyyy custom format
        Assert.Equal("yesterday-ish", sheet.Rows[2].Cells[4]); // text in a date column stays text
        Assert.Equal("self2@fugue.test", sheet.Rows[3].Cells[0]); // rich text runs joined
        Assert.Equal("300504", sheet.Rows[3].Cells[3]); // a number, not a date (no date style)
        Assert.Equal("Shatin", sheet.Rows[3].Cells[5]); // the formula's cached value
        Assert.Equal(string.Empty, sheet.Rows[2].Cells[6]);
    }

    [Fact]
    public void Xlsx_built_by_hand_with_inline_strings_and_a_builtin_date_format_is_read()
    {
        byte[] xlsx = Workbook(
            """<sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>When</t></is></c><c r="C1" t="inlineStr"><is><t>Paid</t></is></c></row><row r="2"><c r="A2" s="1"><v>46349</v></c><c r="C2" t="b"><v>1</v></c></row></sheetData>""",
            """<cellXfs count="2"><xf numFmtId="0"/><xf numFmtId="14"/></cellXfs>""");

        ImportSheet sheet = SpreadsheetReader.ReadXlsx(xlsx).Value;

        Assert.Equal(["When", string.Empty, "Paid"], sheet.Headers); // the gap in B is kept so columns line up
        Assert.Equal(["2026-11-23", string.Empty, "TRUE"], sheet.Rows[0].Cells);
    }

    [Theory]
    [InlineData("not a zip at all")]
    [InlineData("PK\u0003\u0004 truncated")]
    public void Bytes_that_are_not_a_workbook_are_unreadable(string text) =>
        AssertUnreadable(SpreadsheetReader.Read("claims.xlsx", Encoding.Latin1.GetBytes(text)));

    [Fact]
    public void A_zip_without_a_worksheet_or_with_broken_xml_is_unreadable()
    {
        using var empty = new MemoryStream();
        using (var zip = new ZipArchive(empty, ZipArchiveMode.Create, true))
        {
            zip.CreateEntry("hello.txt");
        }

        AssertUnreadable(SpreadsheetReader.ReadXlsx(empty.ToArray()));
        AssertUnreadable(SpreadsheetReader.ReadXlsx(Workbook("<sheetData><row r=\"1\"><c r=\"A1\"", null)));
    }

    [Fact]
    public void Other_extensions_are_not_a_spreadsheet() =>
        AssertUnreadable(SpreadsheetReader.Read("claims.xls", [1, 2, 3]));

    [Fact]
    public void Mbox_splits_messages_decodes_quoted_printable_and_encoded_words_and_drops_the_reply_chain()
    {
        IReadOnlyList<EmailMessage> messages = EmailReader.Read("inbox.mbox", Fixture("inbox.mbox")).Value;

        Assert.Equal(2, messages.Count);
        EmailMessage broker = messages[0];
        Assert.Equal("New claim – water damage", broker.Subject);
        Assert.Equal("Harbour Brokers Claims", broker.FromName);
        Assert.Equal("claims@harbourbrokers.example", broker.FromAddress);
        Assert.Equal(new DateTimeOffset(2026, 11, 27, 10, 15, 0, TimeSpan.FromHours(8)), broker.SentAt);
        Assert.Contains("damaged the kitchen cabinets and flooring – photos to follow.", broker.Body, StringComparison.Ordinal); // soft break + UTF-8
        Assert.DoesNotContain("<p>", broker.Body, StringComparison.Ordinal); // text/plain wins over text/html

        EmailMessage customer = messages[1];
        Assert.Contains("From the other driver", customer.Body, StringComparison.Ordinal); // mboxrd ">From " unescaped
        Assert.DoesNotContain("How can we help", customer.Body, StringComparison.Ordinal); // quoted reply dropped
        Assert.NotEqual(broker.RawKey, customer.RawKey);
    }

    [Fact]
    public void Eml_with_a_base64_body_counts_the_attachment_without_reading_it()
    {
        EmailMessage message = Assert.Single(EmailReader.Read("break-in.eml", Fixture("break-in.eml")).Value);

        Assert.Equal("Break-in at the shop", message.Subject);
        Assert.Equal(1, message.Attachments);
        Assert.Contains("Someone broke into our shophouse in Georgetown last night", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("/9j/", message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void The_email_key_is_the_message_id_so_the_same_email_in_another_export_is_recognised()
    {
        byte[] alone = Encoding.UTF8.GetBytes("From: a@b.sg\nSubject: x\nMessage-ID: <same@b.sg>\n\nbody one\n");
        byte[] resent = Encoding.UTF8.GetBytes("From a@b.sg Mon Nov 30 10:00:00 2026\nFrom: a@b.sg\nSubject: x\nMessage-ID: <same@b.sg>\nX-Forwarded: yes\n\nbody one\n");

        Assert.Equal(EmailReader.Read("a.eml", alone).Value[0].RawKey, EmailReader.Read("b.mbox", resent).Value[0].RawKey);
    }

    [Fact]
    public void Html_only_email_is_reduced_to_text()
    {
        byte[] html = Encoding.UTF8.GetBytes("From: x@y.ph\nSubject: hi\nContent-Type: text/html; charset=utf-8\n\n<html><head><style>p{}</style></head><body><p>Flood&nbsp;in the <b>garage</b></p><p>Policy: POL-PH-9</p></body></html>\n");

        string body = EmailReader.Read("h.eml", html).Value[0].Body;

        Assert.Contains("Flood in the garage", body, StringComparison.Ordinal);
        Assert.Contains("Policy: POL-PH-9", body, StringComparison.Ordinal);
        Assert.DoesNotContain("p{}", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("inbox.mbox", "this is not a mailbox\njust text\n")]
    [InlineData("one.eml", "no headers here\n\nbody\n")]
    [InlineData("one.eml", "")]
    public void Text_that_is_not_email_is_unreadable(string fileName, string text) =>
        AssertUnreadable(EmailReader.Read(fileName, Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void Other_extensions_and_binary_files_are_not_email()
    {
        AssertUnreadable(EmailReader.Read("inbox.pst", Encoding.UTF8.GetBytes("From: a@b\n\nx")));
        AssertUnreadable(EmailReader.Read("inbox.mbox", [0x00, 0x01, 0x02]));
    }

    [Fact]
    public void Email_drafts_route_the_market_by_sender_domain_and_read_the_labelled_fields()
    {
        IReadOnlyList<EmailMessage> messages = EmailReader.Read("inbox.mbox", Fixture("inbox.mbox")).Value;

        IReadOnlyList<ImportDraft> drafts = ImportDrafting.FromEmails(messages, Routing, Today);

        ImportDraft broker = drafts[0];
        Assert.Equal("HK", broker.Market); // the explicit harbourbrokers.example entry
        Assert.Contains(broker.Hints, h => h.Contains("Import:MarketByDomain entry \"harbourbrokers.example\"", StringComparison.Ordinal));
        Assert.Equal("Property", broker.Line);
        Assert.Equal("POL-HK-300201", broker.PolicyNumber);
        Assert.Equal("2026-11-25", broker.IncidentDate);
        Assert.Equal("Flat 12B, Tai Koo Shing, Hong Kong", broker.IncidentLocation);
        Assert.Equal("self@fugue.test", broker.ClaimantEmail); // the insured named in the body, not the broker who sent it
        Assert.Equal(messages[0].RawKey, broker.Key);
        Assert.StartsWith("Email \"New claim – water damage\" from Harbour Brokers Claims <claims@harbourbrokers.example>, sent 2026-11-27 10:15 +08:00.", broker.Note, StringComparison.Ordinal);

        ImportDraft customer = drafts[1];
        Assert.Equal("TH", customer.Market); // "Market: Thailand" in the body; fugue.test has no route
        Assert.Equal("Motor", customer.Line);
        Assert.Equal("POL-TH-300202", customer.PolicyNumber);
        Assert.Equal("2026-11-27", customer.IncidentDate); // "yesterday" in a message sent 2026-11-28
        Assert.Equal("self2@fugue.test", customer.ClaimantEmail);
    }

    [Fact]
    public void An_unrouted_domain_says_so_and_a_longer_suffix_wins()
    {
        Assert.Null(DraftHints.MarketForDomain("gmail.com", Routing));
        Assert.Equal("MY", DraftHints.MarketForDomain("kedai-armenian.com.my", Routing)?.Market);
        var specific = new Dictionary<string, string> { ["com.sg"] = "SG", ["lion.com.sg"] = "MY" };
        Assert.Equal("MY", DraftHints.MarketForDomain("claims.lion.com.sg", specific)?.Market);
        Assert.Equal("SG", DraftHints.MarketForDomain("other.com.sg", specific)?.Market);

        EmailMessage gmail = EmailReader.Read("g.eml", Encoding.UTF8.GetBytes("From: Sam <sam@gmail.com>\nSubject: dent\n\nMy car.\n")).Value[0];
        ImportDraft draft = ImportDrafting.FromEmails([gmail], Routing, Today)[0];
        Assert.Null(draft.Market);
        Assert.Contains(draft.Hints, h => h.StartsWith("No market for the sender domain gmail.com", StringComparison.Ordinal));
    }

    [Fact]
    public void WhatsApp_android_export_reads_senders_multiline_messages_system_lines_and_media()
    {
        ChatTranscript chat = WhatsAppChatReader.Read("WhatsApp Chat with +65 9123 4567.txt", Fixture("WhatsApp Chat with +65 9123 4567.txt")).Value;

        Assert.Equal(7, chat.Messages.Count);
        Assert.Null(chat.Messages[0].Sender); // the encryption notice
        Assert.True(chat.Messages[2].IsMedia);
        Assert.Equal("POL-SG-300301\nmy email is self@fugue.test", chat.Messages[4].Text);
        Assert.Equal(new DateTime(2026, 11, 27, 21, 45, 0), chat.Messages[4].At);
        Assert.True(chat.DayFirst);
    }

    [Fact]
    public void WhatsApp_draft_names_the_claimant_from_the_file_routes_by_calling_code_and_attaches_the_transcript()
    {
        string name = "WhatsApp Chat with +65 9123 4567.txt";
        ImportDraft draft = ImportDrafting.FromChat(WhatsAppChatReader.Read(name, Fixture(name)).Value, name)!;

        Assert.Equal("+65 9123 4567", draft.ClaimantName);
        Assert.Equal("SG", draft.Market);
        Assert.Equal("Motor", draft.Line);
        Assert.Equal("POL-SG-300301", draft.PolicyNumber);
        Assert.Equal("2026-11-27", draft.IncidentDate); // "this evening" in a message sent 2026-11-27
        Assert.Equal("self@fugue.test", draft.ClaimantEmail);
        Assert.DoesNotContain("Fugue Claims", draft.IncidentDescription, StringComparison.Ordinal); // the team's messages are not the description
        Assert.StartsWith($"WhatsApp chat export {name}, 7 messages", draft.Note, StringComparison.Ordinal);
        Assert.Contains("[27/11 21:43] Fugue Claims: Sorry to hear that.", draft.Note, StringComparison.Ordinal);
        Assert.Contains(draft.Hints, h => h.Contains("media item", StringComparison.Ordinal));
    }

    [Fact]
    public void WhatsApp_ios_export_with_seconds_and_am_pm_reads_and_a_later_export_keeps_the_key()
    {
        string first = "‎[3/4/26, 9:05:10 PM] Fugue Claims: Hello\n[3/4/26, 9:06:00 PM] Mei Ling: My flat ceiling is leaking since yesterday\n";
        string later = first + "[13/4/26, 10:00:00 AM] Mei Ling: Any update? Policy POL-HK-7781\n";

        ChatTranscript a = WhatsAppChatReader.Read("WhatsApp Chat - Mei Ling.txt", Encoding.UTF8.GetBytes(first)).Value;
        ChatTranscript b = WhatsAppChatReader.Read("WhatsApp Chat - Mei Ling.txt", Encoding.UTF8.GetBytes(later)).Value;
        ImportDraft da = ImportDrafting.FromChat(a, "WhatsApp Chat - Mei Ling.txt")!;
        ImportDraft db = ImportDrafting.FromChat(b, "WhatsApp Chat - Mei Ling.txt")!;

        Assert.Equal(new DateTime(2026, 4, 3, 21, 6, 0), a.Messages[1].At);
        Assert.Equal("Mei Ling", da.ClaimantName); // named by the file, although the team wrote first
        Assert.Equal("Property", da.Line);
        Assert.Equal("2026-04-02", da.IncidentDate);
        Assert.Equal(da.Key, db.Key);
        Assert.Equal("POL-HK-7781", db.PolicyNumber);
    }

    [Fact]
    public void WhatsApp_month_first_export_is_detected()
    {
        ChatTranscript chat = WhatsAppChatReader.Read("c.txt", Encoding.UTF8.GetBytes("11/27/26, 21:40 - Ann: hi\n11/28/26, 08:00 - Ann: car dented\n")).Value;

        Assert.False(chat.DayFirst);
        Assert.Equal(new DateTime(2026, 11, 28, 8, 0, 0), chat.Messages[1].At);
    }

    [Theory]
    [InlineData("hello there\nthis is just text\n")]
    [InlineData("")]
    [InlineData("31/02/2026, 10:00 - Ann: impossible date\n")]
    public void Text_that_is_not_a_chat_export_is_unreadable(string text) =>
        AssertUnreadable(WhatsAppChatReader.Read("chat.txt", Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void A_chat_of_only_system_lines_has_no_draft()
    {
        ChatTranscript chat = WhatsAppChatReader.Read("c.txt", Encoding.UTF8.GetBytes("27/11/2026, 21:40 - Messages and calls are end-to-end encrypted.\n")).Value;

        Assert.Null(ImportDrafting.FromChat(chat, "c.txt"));
    }

    [Fact]
    public void A_long_chat_is_cut_to_fit_the_timeline_note_and_says_how_many_messages_were_left_out()
    {
        var text = new StringBuilder();
        for (int i = 0; i < 400; i++)
        {
            text.Append(System.Globalization.CultureInfo.InvariantCulture, $"27/11/2026, 10:{i % 60:D2} - Ann: message number {i} about the dented car door\n");
        }

        ImportDraft draft = ImportDrafting.FromChat(WhatsAppChatReader.Read("c.txt", Encoding.UTF8.GetBytes(text.ToString())).Value, "c.txt")!;

        Assert.True(draft.Note!.Length <= ImportLimits.MaxNoteChars);
        Assert.Matches(@"\.\.\. \d+ more messages in the file\.$", draft.Note);
        Assert.True(draft.IncidentDescription!.Length <= ImportLimits.MaxDescriptionChars);
    }

    [Theory]
    [InlineData("Date of incident: 25/11/2026", "2026-11-25")]
    [InlineData("it happened on 3 Nov", "2026-11-03")]
    [InlineData("Incident date - Dec 30", "2025-12-30")] // no year: the latest date not after the message
    [InlineData("see 2026-10-01 report", "2026-10-01")]
    [InlineData("on 31/12/2027 it will", null)] // after the message, so skipped
    public void Incident_dates_are_read_day_first_and_never_after_the_message(string text, string? expected) =>
        Assert.Equal(expected, DraftHints.IncidentDate(text, Today)?.Iso);

    [Theory]
    [InlineData("My policy is valid until May", null)] // no digit, not a policy number
    [InlineData("policy no. abc-123.", "ABC-123")]
    [InlineData("ref pol-sg-100200 thanks", "POL-SG-100200")]
    public void Policy_numbers_need_a_digit(string text, string? expected) =>
        Assert.Equal(expected, DraftHints.Policy(text)?.Value);

    private static byte[] Workbook(string sheetData, string? cellXfs)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            Write(zip, "xl/workbook.xml", """<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="S" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Write(zip, "xl/_rels/workbook.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Target="worksheets/sheet1.xml" Type="worksheet"/></Relationships>""");
            Write(zip, "xl/worksheets/sheet1.xml", $"""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">{sheetData}</worksheet>""");
            if (cellXfs is not null)
            {
                Write(zip, "xl/styles.xml", $"""<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">{cellXfs}</styleSheet>""");
            }
        }

        return stream.ToArray();
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
