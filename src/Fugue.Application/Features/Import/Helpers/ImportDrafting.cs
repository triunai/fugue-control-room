using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Fugue.Application.Features.Import.Models;

namespace Fugue.Application.Features.Import.Helpers;

/// <summary>Turns parsed emails and chats into <see cref="ImportDraft"/>s. Spreadsheet rows become drafts on the client, through the column mapping.</summary>
public static partial class ImportDrafting
{
    /// <summary>One draft per email. The market comes from a "Market:" line in the body, else from the sender domain through the configured table.</summary>
    public static IReadOnlyList<ImportDraft> FromEmails(IReadOnlyList<EmailMessage> messages, IReadOnlyDictionary<string, string> marketByDomain, DateOnly today)
    {
        List<ImportDraft> drafts = [];
        for (int i = 0; i < messages.Count; i++)
        {
            EmailMessage m = messages[i];
            List<string> hints = [];
            string subject = m.Subject ?? string.Empty;
            string all = subject + "\n" + m.Body;
            DateOnly sentOn = m.SentAt is { } at ? DateOnly.FromDateTime(at.DateTime) : today;

            string? market = Take(DraftHints.MarketInText(m.Body), hints);
            if (market is null && m.FromAddress is { } address && address.LastIndexOf('@') is var atSign and > 0)
            {
                string domain = address[(atSign + 1)..];
                market = Take(DraftHints.MarketForDomain(domain, marketByDomain), hints);
                if (market is null)
                {
                    hints.Add($"No market for the sender domain {domain.ToLowerInvariant()} in Import:MarketByDomain; choose one.");
                }
            }

            string description = m.Body.Length > 0 ? m.Body : subject;
            if (description.Length > ImportLimits.MaxDescriptionChars)
            {
                description = description[..ImportLimits.MaxDescriptionChars];
                hints.Add($"The body was cut to {ImportLimits.MaxDescriptionChars} characters for the description.");
            }

            string sent = m.SentAt is { } s ? s.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture) : "with no Date header";
            string from = m.FromName is { } name ? $"{name} <{m.FromAddress}>" : m.FromAddress ?? "an unknown sender";
            string note = $"Email \"{subject}\" from {from}, sent {sent}."
                + (m.Attachments > 0 ? $" {m.Attachments} attachment{(m.Attachments == 1 ? string.Empty : "s")} not imported." : string.Empty);
            if (m.SentAt is null)
            {
                hints.Add("The email has no Date header; relative dates were counted from today.");
            }

            drafts.Add(new ImportDraft(
                m.RawKey,
                ImportText.Cap($"Email {i + 1} of {messages.Count}: {(subject.Length > 0 ? subject : "(no subject)")}", 200),
                market,
                Take(DraftHints.Line(all), hints),
                Take(DraftHints.Policy(all), hints),
                Take(DraftHints.IncidentDate(m.Body, sentOn), hints),
                description,
                DraftHints.Location(m.Body),
                Take(DraftHints.ClaimantEmailInText(m.Body), hints) ?? m.FromAddress,
                m.FromName,
                note,
                hints));
        }

        return drafts;
    }

    /// <summary>
    /// One draft per chat. The claimant is the person named in the export's file name ("WhatsApp Chat with Mei Ling.txt", the export is taken from the
    /// business phone) when they wrote in it, else the first sender. The description is what the claimant wrote; the whole chat becomes the note.
    /// The key is the claimant's first message, so a later export of the same conversation (with more messages) is still recognised.
    /// </summary>
    public static ImportDraft? FromChat(ChatTranscript chat, string fileName)
    {
        List<ChatMessage> spoken = chat.Messages.Where(m => m.Sender is not null).ToList();
        if (spoken.Count == 0)
        {
            return null;
        }

        List<string> hints = [chat.DateOrderHint];
        string? named = ChatWith().Match(Path.GetFileNameWithoutExtension(fileName)) is { Success: true } w ? w.Groups["name"].Value.Trim() : null;
        string claimant;
        if (named is not null && spoken.Any(m => m.Sender!.Equals(named, StringComparison.OrdinalIgnoreCase)))
        {
            claimant = spoken.First(m => m.Sender!.Equals(named, StringComparison.OrdinalIgnoreCase)).Sender!;
            hints.Add($"Claimant {claimant}: the export is the chat with them (file name).");
        }
        else
        {
            claimant = spoken[0].Sender!;
            hints.Add($"Claimant {claimant}: the first person who wrote; check this is the customer, not the team.");
        }

        List<ChatMessage> theirs = spoken.Where(m => m.Sender == claimant).ToList();
        ChatMessage first = theirs[0];
        string theirText = string.Join('\n', theirs.Where(m => !m.IsMedia).Select(m => m.Text));
        string everything = string.Join('\n', spoken.Where(m => !m.IsMedia).Select(m => m.Text));
        DateOnly sentOn = DateOnly.FromDateTime(first.At);

        string description = theirText;
        if (description.Length > ImportLimits.MaxDescriptionChars)
        {
            description = description[..ImportLimits.MaxDescriptionChars];
            hints.Add($"The claimant's messages were cut to {ImportLimits.MaxDescriptionChars} characters for the description.");
        }

        int media = chat.Messages.Count(m => m.IsMedia);
        if (media > 0)
        {
            hints.Add($"{media} media item{(media == 1 ? string.Empty : "s")} in the chat; the export has no files, ask for them if needed.");
        }

        string? market = Take(DraftHints.MarketForPhone(claimant), hints) ?? Take(DraftHints.MarketInText(everything), hints);
        string key = ImportText.Sha256(string.Create(CultureInfo.InvariantCulture, $"whatsapp\n{first.At:yyyy-MM-ddTHH:mm}\n{first.Sender}\n{first.Text}"));
        return new ImportDraft(
            key,
            ImportText.Cap($"Chat with {claimant}, {chat.Messages.Count} messages from {chat.Messages[0].At:yyyy-MM-dd}", 200),
            market,
            Take(DraftHints.Line(everything), hints),
            Take(DraftHints.Policy(everything), hints),
            Take(DraftHints.IncidentDate(theirText, sentOn), hints),
            description,
            DraftHints.Location(everything),
            DraftHints.Email(theirText),
            claimant,
            Transcript(chat, fileName, hints),
            hints);
    }

    /// <summary>The chat as a note: a header line, then one line per message, cut to fit the timeline note with a count of what was left out.</summary>
    private static string Transcript(ChatTranscript chat, string fileName, List<string> hints)
    {
        var note = new StringBuilder(string.Create(
            CultureInfo.InvariantCulture,
            $"WhatsApp chat export {Path.GetFileName(fileName)}, {chat.Messages.Count} messages, {chat.Messages[0].At:yyyy-MM-dd HH:mm} to {chat.Messages[^1].At:yyyy-MM-dd HH:mm}:"));
        for (int i = 0; i < chat.Messages.Count; i++)
        {
            ChatMessage m = chat.Messages[i];
            string line = string.Create(CultureInfo.InvariantCulture, $"\n[{m.At:dd/MM HH:mm}] {(m.Sender is null ? string.Empty : m.Sender + ": ")}{m.Text}");
            if (note.Length + line.Length > ImportLimits.MaxNoteChars - 40)
            {
                int left = chat.Messages.Count - i;
                note.Append(CultureInfo.InvariantCulture, $"\n... {left} more message{(left == 1 ? string.Empty : "s")} in the file.");
                hints.Add($"The transcript note holds the first {i} of {chat.Messages.Count} messages.");
                break;
            }

            note.Append(line);
        }

        return note.ToString();
    }

    private static string? Take((string Value, string Hint)? found, List<string> hints)
    {
        if (found is not { } f)
        {
            return null;
        }

        hints.Add(f.Hint);
        return f.Value;
    }

    [GeneratedRegex(@"^WhatsApp Chat (?:with|-)\s+(?<name>.+?)(?:\s*\(\d+\))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChatWith();
}
