using System.Globalization;
using System.Text;
using Fugue.Application.Features.Letters.Models;

namespace Fugue.Application.Features.Letters.Helpers;

/// <summary>
/// The one fixed, branded letter template (Fugue Console palette: navy, cream, gold; serif heading), written as a single-page PDF 1.4 by hand.
/// No PDF library: the format needed here is a page of rectangles and text in the standard Type 1 fonts (Times-Bold, Helvetica), which every
/// viewer ships, so nothing is embedded and no licence is taken on (D-row in the nth-pdf report). The content stream is left uncompressed on
/// purpose: it is a few kilobytes, and tests can read the text back. Text outside Windows-1252/Latin-1 prints as "?".
/// </summary>
public static class ClaimLetterPdf
{
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double Left = 56;
    private const double Right = PageWidth - 56;
    private const double Width = Right - Left;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // Fugue Console tokens (web/src/styles/tokens.css): --accent, --bg/--surface, --gold, --gold-text, --text, --muted, --line.
    private static readonly string Navy = Rgb(0x22, 0x40, 0x6b);
    private static readonly string Cream = Rgb(0xfb, 0xf8, 0xf1);
    private static readonly string CreamText = Rgb(0xf7, 0xf3, 0xea);
    private static readonly string Gold = Rgb(0xb6, 0x9a, 0x65);
    private static readonly string GoldText = Rgb(0xd9, 0xc3, 0x92);
    private static readonly string GoldTint = Rgb(0xf1, 0xe8, 0xd4);
    private static readonly string Ink = Rgb(0x1e, 0x1b, 0x16);
    private static readonly string Muted = Rgb(0x6e, 0x67, 0x5a);
    private static readonly string Rule = Rgb(0xdd, 0xd4, 0xc2);

    // Helvetica advance widths (Adobe AFM, 1/1000 em) for ASCII 32..126; other characters measure as 556. Used to wrap and right-align.
    private static readonly short[] HelveticaWidths =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278, 556, 556, 556, 556, 556, 556, 556, 556, 556, 556,
        278, 278, 584, 584, 584, 556, 1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778, 667, 778, 722, 667,
        611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556, 333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833,
        556, 556, 556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584,
    ];

    private enum Font
    {
        Serif,
        Sans,
        SansBold,
    }

    /// <summary>The letter as PDF bytes (starts with <c>%PDF-</c>).</summary>
    public static byte[] Render(ClaimLetter letter)
    {
        var page = new StringBuilder();

        // Frame: cream page, navy band with the wordmark, gold rule.
        Box(page, Cream, 0, 0, PageWidth, PageHeight);
        Box(page, Navy, 0, 772, PageWidth, 70);
        Box(page, Gold, 0, 768, PageWidth, 4);
        Text(page, Font.Serif, 26, GoldText, Left, 800, "FUGUE");
        Text(page, Font.Sans, 9, CreamText, Left, 784, "Claims Console");
        TextRight(page, Font.Sans, 9, CreamText, Right, 800, letter.Kind == ClaimLetterKind.Settlement ? "Settlement letter" : "Decision letter");
        TextRight(page, Font.Sans, 9, CreamText, Right, 784, letter.ClaimNumber);

        // The sample label, unmissable.
        Box(page, GoldTint, Left, 728, Width, 24);
        Stroke(page, Gold, 0.75, Left, 728, Width, 24);
        Text(page, Font.SansBold, 9, Navy, Left + 10, 737, ClaimLetter.SampleLabel.ToUpperInvariant());
        TextRight(page, Font.Sans, 8, Muted, Right - 10, 737, "For demonstration only");

        Text(page, Font.Sans, 10, Ink, Left, 702, letter.LetterDate);
        TextRight(page, Font.Sans, 10, Ink, Right, 702, $"Our reference: {letter.ClaimNumber}");
        Text(page, Font.Sans, 8, Muted, Left, 680, "TO");
        Text(page, Font.SansBold, 11, Ink, Left, 666, letter.Recipient);

        Text(page, Font.Serif, 26, Navy, Left, 626, letter.Title);
        Text(page, Font.Sans, 10, Muted, Left, 608, letter.Subject);
        Box(page, Gold, Left, 598, 48, 2);

        double y = Paragraph(page, Font.Sans, 11, Ink, 576, 15, letter.Summary, 4);

        y = Heading(page, y - 14, "Claim details");
        y = Details(page, y, letter.Details);

        y = Heading(page, y - 8, "Reason for the decision");
        y = Paragraph(page, Font.Sans, 10.5, Ink, y, 14, letter.DecisionReason, 6);

        y = Heading(page, y - 6, "What you told us");
        y = Paragraph(page, Font.Sans, 10.5, Ink, y, 14, letter.IncidentDescription, 4);

        y -= 16;
        Text(page, Font.Sans, 10.5, Ink, Left, y, "Yours sincerely,");
        Text(page, Font.SansBold, 11, Ink, Left, y - 18, letter.Signatory);
        Text(page, Font.Sans, 9, Muted, Left, y - 31, "Claims team, Fugue");

        // Footer: the label again, when it was made and the request that made it.
        page.Append(Invariant, $"{Gold} RG 0.5 w {Left} 56 m {Right} 56 l S\n");
        Text(page, Font.SansBold, 8, Navy, Left, 42, $"{ClaimLetter.SampleLabel}.");
        Text(page, Font.Sans, 8, Muted, Left + Measure(Font.SansBold, 8, $"{ClaimLetter.SampleLabel}. "), 42,
            "No payment, cover or liability follows from it.");
        Text(page, Font.Sans, 8, Muted, Left, 30, $"Generated {letter.GeneratedAt}  |  Request correlation id {letter.CorrelationId}");
        Box(page, Navy, 0, 0, PageWidth, 8);

        return Assemble(page.ToString(), $"{letter.Title} {letter.ClaimNumber}");
    }

    private static double Heading(StringBuilder page, double y, string text)
    {
        Text(page, Font.Serif, 13, Navy, Left, y, text);
        return y - 18;
    }

    /// <summary>Two columns of label and value rows with a hairline under each row.</summary>
    private static double Details(StringBuilder page, double y, IReadOnlyList<ClaimLetterRow> rows)
    {
        const double column = Width / 2;
        const double labelWidth = 92;
        for (int i = 0; i < rows.Count; i += 2)
        {
            for (int c = 0; c < 2 && i + c < rows.Count; c++)
            {
                double x = Left + (c * column);
                Text(page, Font.Sans, 8.5, Muted, x, y, rows[i + c].Label);
                Text(page, Font.Sans, 10, Ink, x + labelWidth, y, Fit(Font.Sans, 10, rows[i + c].Value, column - labelWidth - 12));
            }

            page.Append(Invariant, $"{Rule} RG 0.5 w {Left} {y - 6:0.##} m {Right} {y - 6:0.##} l S\n");
            y -= 19;
        }

        return y;
    }

    /// <summary>Word-wrapped text from baseline <paramref name="y"/>; more than <paramref name="maxLines"/> lines end in "...". Returns the next baseline.</summary>
    private static double Paragraph(StringBuilder page, Font font, double size, string color, double y, double leading, string text, int maxLines)
    {
        List<string> lines = Wrap(font, size, text, Width);
        if (lines.Count > maxLines)
        {
            lines = lines.Take(maxLines).ToList();
            lines[^1] = Fit(font, size, lines[^1] + " ...", Width);
        }

        foreach (string line in lines)
        {
            Text(page, font, size, color, Left, y, line);
            y -= leading;
        }

        return y - 4;
    }

    private static List<string> Wrap(Font font, double size, string text, double width)
    {
        var lines = new List<string>();
        var line = new StringBuilder();
        foreach (string word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = line.Length == 0 ? word : $"{line} {word}";
            if (Measure(font, size, candidate) <= width)
            {
                line.Clear().Append(candidate);
                continue;
            }

            if (line.Length > 0)
            {
                lines.Add(line.ToString());
            }

            line.Clear().Append(Fit(font, size, word, width));
        }

        if (line.Length > 0)
        {
            lines.Add(line.ToString());
        }

        return lines;
    }

    /// <summary>The text, cut and ended with "..." when it is wider than <paramref name="width"/>.</summary>
    private static string Fit(Font font, double size, string text, double width)
    {
        if (Measure(font, size, text) <= width)
        {
            return text;
        }

        string cut = text;
        while (cut.Length > 0 && Measure(font, size, cut + "...") > width)
        {
            cut = cut[..^1];
        }

        return cut.TrimEnd() + "...";
    }

    private static double Measure(Font font, double size, string text)
    {
        double units = 0;
        foreach (char ch in text)
        {
            units += ch is >= ' ' and <= '~' ? HelveticaWidths[ch - ' '] : 556;
        }

        // Bold and the serif face run a little wider than regular Helvetica; a margin keeps wrapped and right-aligned text inside the page.
        double scale = font == Font.Sans ? 1.0 : 1.08;
        return units * scale * size / 1000;
    }

    private static void Box(StringBuilder page, string color, double x, double y, double w, double h) =>
        page.Append(Invariant, $"{color} rg {x:0.##} {y:0.##} {w:0.##} {h:0.##} re f\n");

    private static void Stroke(StringBuilder page, string color, double lineWidth, double x, double y, double w, double h) =>
        page.Append(Invariant, $"{color} RG {lineWidth:0.##} w {x:0.##} {y:0.##} {w:0.##} {h:0.##} re S\n");

    private static void TextRight(StringBuilder page, Font font, double size, string color, double right, double y, string text) =>
        Text(page, font, size, color, right - Measure(font, size, text), y, text);

    private static void Text(StringBuilder page, Font font, double size, string color, double x, double y, string text)
    {
        string name = font switch
        {
            Font.Serif => "F1",
            Font.Sans => "F2",
            _ => "F3",
        };
        page.Append(Invariant, $"BT /{name} {size:0.##} Tf {color} rg {x:0.##} {y:0.##} Td ({Escape(text)}) Tj ET\n");
    }

    /// <summary>A PDF literal string body: Latin-1 only (anything else is "?", control characters a space), with <c>\ ( )</c> escaped.</summary>
    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char ch in text)
        {
            char c = ch switch
            {
                '‘' or '’' => '\'',
                '“' or '”' => '"',
                '–' or '—' => '-',
                < ' ' or (>= '\u007F' and < ' ') => ' ',
                > 'ÿ' => '?',
                _ => ch,
            };
            if (c is '\\' or '(' or ')')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string Rgb(int r, int g, int b) => string.Create(Invariant, $"{r / 255.0:0.###} {g / 255.0:0.###} {b / 255.0:0.###}");

    /// <summary>Catalog, page tree, one page, its content stream, three standard fonts and the info dictionary, with a byte-exact xref table.</summary>
    private static byte[] Assemble(string content, string title)
    {
        Encoding latin1 = Encoding.Latin1;
        byte[] stream = latin1.GetBytes(content);
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R /F2 6 0 R /F3 7 0 R >> >> /Contents 4 0 R >>",
            string.Empty, // the content stream, written below
            "<< /Type /Font /Subtype /Type1 /BaseFont /Times-Bold /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
            $"<< /Title ({Escape(title)}) /Subject ({Escape(ClaimLetter.SampleLabel)}) /Producer (Fugue sample letter template) >>",
        ];

        using var output = new MemoryStream();
        void Write(string s) => output.Write(latin1.GetBytes(s));

        Write("%PDF-1.4\n%âãÏÓ\n");
        var offsets = new long[objects.Length];
        for (int i = 0; i < objects.Length; i++)
        {
            offsets[i] = output.Position;
            int number = i + 1;
            if (number == 4)
            {
                Write(string.Create(Invariant, $"4 0 obj\n<< /Length {stream.Length} >>\nstream\n"));
                output.Write(stream);
                Write("\nendstream\nendobj\n");
            }
            else
            {
                Write(string.Create(Invariant, $"{number} 0 obj\n{objects[i]}\nendobj\n"));
            }
        }

        long xref = output.Position;
        var table = new StringBuilder();
        table.Append(Invariant, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (long offset in offsets)
        {
            table.Append(Invariant, $"{offset:D10} 00000 n \n");
        }

        table.Append(Invariant, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R /Info 8 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        Write(table.ToString());
        return output.ToArray();
    }
}
