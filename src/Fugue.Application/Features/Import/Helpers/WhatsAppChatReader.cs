using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentResults;
using Fugue.Application.Features.Import.Models;

namespace Fugue.Application.Features.Import.Helpers;

/// <summary>One chat message. <paramref name="Sender"/> is null for a system line ("Messages and calls are end-to-end encrypted").</summary>
public sealed record ChatMessage(DateTime At, string? Sender, string Text, bool IsMedia);

/// <summary>A chat export as read, with how the dates were read (exports do not say whether 03/04 is 3 April or 4 March).</summary>
public sealed record ChatTranscript(IReadOnlyList<ChatMessage> Messages, bool DayFirst, string DateOrderHint);

/// <summary>
/// Reads a WhatsApp "Export chat" .txt (without media), in both shapes the apps write: Android <c>28/11/2026, 09:14 - Name: text</c> and iOS
/// <c>[28/11/2026, 09:14:05] Name: text</c>, 24-hour or AM/PM, with continuation lines for multi-line messages and system lines without a sender.
/// </summary>
public static partial class WhatsAppChatReader
{
    private static readonly string[] MediaMarkers =
        ["<media omitted>", "<attached:", "(file attached)", "image omitted", "video omitted", "audio omitted", "sticker omitted", "document omitted", "gif omitted"];

    public static Result<ChatTranscript> Read(string fileName, byte[] bytes)
    {
        if (!Path.GetExtension(fileName).Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return ImportText.Unreadable<ChatTranscript>("A WhatsApp import takes the .txt file of an \"Export chat\" (without media).");
        }

        if (ImportText.LooksBinary(bytes))
        {
            return ImportText.Unreadable<ChatTranscript>("This is not a text chat export.");
        }

        string text = ImportText.Decode(bytes);
        List<(Match Head, StringBuilder Text)> raw = [];
        foreach (string rawLine in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string line = rawLine.TrimStart('‎', '‏', '﻿').Replace(' ', ' ').Replace(' ', ' ');
            Match head = Line().Match(line);
            if (head.Success)
            {
                raw.Add((head, new StringBuilder(head.Groups["rest"].Value)));
                if (raw.Count > ImportLimits.MaxRows)
                {
                    return ImportText.Unreadable<ChatTranscript>($"A chat import takes at most {ImportLimits.MaxRows} messages.");
                }
            }
            else if (raw.Count > 0)
            {
                raw[^1].Text.Append('\n').Append(line);
            }
            else if (line.Trim().Length > 0)
            {
                return ImportText.Unreadable<ChatTranscript>("This does not look like a WhatsApp chat export (each message starts with a date and time).");
            }
        }

        if (raw.Count == 0)
        {
            return ImportText.Unreadable<ChatTranscript>("The chat export has no messages.");
        }

        // A first number above 12 proves day-first, a second above 12 proves month-first; with neither, day-first (the six markets write dates that way).
        bool dayFirst = !raw.Any(r => Int(r.Head, "b") > 12) || raw.Any(r => Int(r.Head, "a") > 12);
        string hint = raw.Any(r => Int(r.Head, "a") > 12) ? "Dates read day-first (a day above 12 appears in the export)."
            : raw.Any(r => Int(r.Head, "b") > 12) ? "Dates read month-first (a day above 12 appears second in the export)."
            : "Dates read day-first, as in all six markets; the export alone cannot prove the order.";

        List<ChatMessage> messages = [];
        foreach ((Match head, StringBuilder body) in raw)
        {
            if (At(head, dayFirst) is not { } at)
            {
                return ImportText.Unreadable<ChatTranscript>($"The date on the line \"{head.Value.Trim()}\" cannot be read.");
            }

            string rest = body.ToString().TrimEnd();
            int colon = rest.IndexOf(": ", StringComparison.Ordinal);
            string? sender = colon > 0 && colon <= 80 ? rest[..colon].Trim() : null;
            string message = sender is null ? rest.Trim() : rest[(colon + 2)..].Trim();
            string lower = message.ToLowerInvariant();
            messages.Add(new ChatMessage(at, sender, message, MediaMarkers.Any(m => lower.Contains(m, StringComparison.Ordinal))));
        }

        return Result.Ok(new ChatTranscript(messages, dayFirst, hint));
    }

    private static int Int(Match m, string group) => int.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture);

    private static DateTime? At(Match m, bool dayFirst)
    {
        int a = Int(m, "a");
        int b = Int(m, "b");
        int year = Int(m, "y");
        year = year < 100 ? 2000 + year : year;
        (int day, int month) = dayFirst ? (a, b) : (b, a);
        int hour = Int(m, "h");
        int minute = Int(m, "min");
        int second = m.Groups["s"].Success ? Int(m, "s") : 0;
        if (m.Groups["ampm"].Success)
        {
            bool pm = m.Groups["ampm"].Value.StartsWith('p') || m.Groups["ampm"].Value.StartsWith('P');
            hour = hour % 12 + (pm ? 12 : 0);
        }

        if (month is < 1 or > 12 || day < 1 || year is < 2009 or > 2999 || day > DateTime.DaysInMonth(year, month) || hour > 23 || minute > 59 || second > 59)
        {
            return null;
        }

        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
    }

    [GeneratedRegex(
        @"^\[?(?<a>\d{1,2})[/.-](?<b>\d{1,2})[/.-](?<y>\d{2}|\d{4}),?\s+(?<h>\d{1,2})[:.](?<min>\d{2})(?:[:.](?<s>\d{2}))?\s?(?<ampm>[AaPp]\.?[Mm]\.?)?(?:\]\s|\s[-–]\s)(?<rest>.*)$",
        RegexOptions.CultureInvariant, 200)]
    private static partial Regex Line();
}
