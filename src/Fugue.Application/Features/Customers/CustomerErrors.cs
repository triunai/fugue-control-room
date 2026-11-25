using Fugue.Domain.Ids;
using Fugue.Domain.Results;

namespace Fugue.Application.Features.Customers;

/// <summary>The stable error codes of the Customers slice.</summary>
public static class CustomerErrors
{
    public static FugueError NotFound(CustomerId id) =>
        FugueErrors.NotFound("customer.not_found", $"Customer {id} was not found.");

    public static FugueError EmailTaken() =>
        FugueErrors.Conflict("customer.email_taken", "A customer with this email already exists.");

    /// <summary>A self-service caller tried to change the email (staff-only, Codex finding 5).</summary>
    public static FugueError EmailChangeForbidden() =>
        FugueErrors.Forbidden("customer.email_change_forbidden", "Only staff can change a customer's email.");

    public static FugueError OwnerNotFound() =>
        FugueErrors.Validation(new Dictionary<string, string[]> { ["userId"] = ["The owner is not a known user."] });

    public static FugueError VersionMismatch() =>
        FugueErrors.PreconditionFailed(
            "customer.version_mismatch", "The customer changed since it was read. Re-read it and retry with the new ETag.");

    public static FugueError IfMatchRequired() =>
        FugueErrors.PreconditionRequired("customer.if_match_required", "This operation requires an If-Match header with the current ETag.");
}
