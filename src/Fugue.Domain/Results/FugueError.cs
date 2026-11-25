using FluentResults;

namespace Fugue.Domain.Results;

/// <summary>
/// The one error type handlers return. It extends <see cref="Error"/> with a stable machine-readable
/// <see cref="Code"/>, a transport-agnostic <see cref="Type"/> (the API layer maps it to an HTTP status)
/// and optional per-field messages for validation failures. Code and type are also copied into
/// <see cref="ResultBase.Errors"/> metadata so generic FluentResults tooling can read them.
/// </summary>
public sealed class FugueError : Error
{
    public const string CodeKey = "code";
    public const string TypeKey = "type";

    public FugueError(string code, string message, ErrorType type, IReadOnlyDictionary<string, string[]>? fields = null)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Type = type;
        Fields = fields;
        Metadata[CodeKey] = code;
        Metadata[TypeKey] = type;
    }

    /// <summary>Stable machine-readable identifier, for example <c>product.not_found</c>.</summary>
    public string Code { get; }

    public ErrorType Type { get; }

    /// <summary>Per-field messages, keyed by field name. Set for <see cref="ErrorType.Validation"/>.</summary>
    public IReadOnlyDictionary<string, string[]>? Fields { get; }
}
