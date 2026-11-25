namespace Fugue.Application.Abstractions;

/// <summary>The accepted shape of a correlation or saga id: 1 to 64 characters of <c>[A-Za-z0-9-_]</c>.</summary>
public static class CorrelationIdFormat
{
    public const int MaxLength = 64;

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        foreach (char c in value)
        {
            bool ok = c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }
}
