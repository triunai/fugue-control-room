using FluentResults;
using FluentValidation;
using Fugue.Application.Features.Import.Helpers;
using Fugue.Application.Features.Import.Models;
using Fugue.Application.Features.Import.Repos;
using Fugue.Domain.Claims;
using Mediator;

namespace Fugue.Application.Features.Import.Queries;

/// <summary>
/// Contract. Reads an uploaded file into rows (spreadsheet) or drafts (inbox export, chat export). Nothing is written: drafts stay with the reviewer
/// until they create a claim from one. Items already imported come back marked with the claim they became.
/// </summary>
public sealed record ReadImportFileQuery(ImportSource Source, string FileName, byte[] Content) : IQuery<Result<ImportFileDto>>;

public sealed class ReadImportFileValidator : AbstractValidator<ReadImportFileQuery>
{
    public ReadImportFileValidator()
    {
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("The file is empty.")
            .Must(c => c.Length <= ImportLimits.MaxFileBytes).WithMessage($"A file import takes at most {ImportLimits.MaxFileBytes / (1024 * 1024)} MB.");
    }
}

public sealed class ReadImportFileHandler(IImportStore imports, IImportSettings settings, TimeProvider clock)
    : IQueryHandler<ReadImportFileQuery, Result<ImportFileDto>>
{
    public async ValueTask<Result<ImportFileDto>> Handle(ReadImportFileQuery query, CancellationToken cancellationToken)
    {
        string fileName = Path.GetFileName(query.FileName);
        string sha = ImportText.Sha256(query.Content);
        if (query.Source == ImportSource.Spreadsheet)
        {
            Result<ImportSheet> sheet = SpreadsheetReader.Read(fileName, query.Content);
            if (sheet.IsFailed)
            {
                return Result.Fail<ImportFileDto>(sheet.Errors);
            }

            IReadOnlyDictionary<string, ImportedRef> done = await imports.FindImportedAsync([.. sheet.Value.Rows.Select(r => r.Key).Distinct()], cancellationToken);
            var rows = sheet.Value.Rows.Select(r => new ImportSheetRowDto(r.Number, r.Key, r.Cells, Ref(done, r.Key))).ToList();
            return Result.Ok(new ImportFileDto(query.Source, fileName, sha, new ImportSheetDto(sheet.Value.Headers, rows), []));
        }

        Result<IReadOnlyList<ImportDraft>> drafts = query.Source == ImportSource.Email ? EmailDrafts(fileName, query.Content) : ChatDrafts(fileName, query.Content);
        if (drafts.IsFailed)
        {
            return Result.Fail<ImportFileDto>(drafts.Errors);
        }

        IReadOnlyDictionary<string, ImportedRef> imported = await imports.FindImportedAsync([.. drafts.Value.Select(d => d.Key).Distinct()], cancellationToken);
        return Result.Ok(new ImportFileDto(query.Source, fileName, sha, null, [.. drafts.Value.Select(d => ImportDraftDto.From(d, Ref(imported, d.Key)))]));
    }

    private static ImportedRefDto? Ref(IReadOnlyDictionary<string, ImportedRef> done, string key) =>
        done.TryGetValue(key, out ImportedRef? r) ? new ImportedRefDto(r.ClaimId, r.ClaimNumber) : null;

    private Result<IReadOnlyList<ImportDraft>> EmailDrafts(string fileName, byte[] content)
    {
        Result<IReadOnlyList<EmailMessage>> messages = EmailReader.Read(fileName, content);
        return messages.IsFailed
            ? Result.Fail<IReadOnlyList<ImportDraft>>(messages.Errors)
            : Result.Ok(ImportDrafting.FromEmails(messages.Value, settings.MarketByDomain, Markets.LocalToday(Market.SG, clock.GetUtcNow())));
    }

    private static Result<IReadOnlyList<ImportDraft>> ChatDrafts(string fileName, byte[] content)
    {
        Result<ChatTranscript> chat = WhatsAppChatReader.Read(fileName, content);
        if (chat.IsFailed)
        {
            return Result.Fail<IReadOnlyList<ImportDraft>>(chat.Errors);
        }

        return ImportDrafting.FromChat(chat.Value, fileName) is { } draft
            ? Result.Ok<IReadOnlyList<ImportDraft>>([draft])
            : ImportText.Unreadable<IReadOnlyList<ImportDraft>>("The chat export has only system lines, no message from a person.");
    }
}
