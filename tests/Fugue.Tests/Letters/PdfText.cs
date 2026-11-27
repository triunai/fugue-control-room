using System.Text;
using System.Text.RegularExpressions;

namespace Fugue.Tests.Letters;

/// <summary>
/// Reads the shown text back out of a letter PDF: the literal strings of every <c>Tj</c> in the (uncompressed) content stream, unescaped, one per
/// line. Enough for the template, which writes each run of text with one <c>Tj</c>; not a general PDF parser.
/// </summary>
internal static partial class PdfText
{
    public static string Extract(byte[] pdf)
    {
        string raw = Encoding.Latin1.GetString(pdf);
        int start = raw.IndexOf("stream\n", StringComparison.Ordinal);
        int end = raw.IndexOf("\nendstream", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "the PDF has no content stream");
        string content = raw[(start + "stream\n".Length)..end];

        var text = new StringBuilder();
        foreach (Match m in ShownString().Matches(content))
        {
            text.AppendLine(Regex.Replace(m.Groups[1].Value, @"\\(.)", "$1"));
        }

        return text.ToString();
    }

    [GeneratedRegex(@"\(((?:\\.|[^\\)])*)\) Tj")]
    private static partial Regex ShownString();
}
