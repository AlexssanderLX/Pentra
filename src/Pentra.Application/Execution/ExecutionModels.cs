using System.Security.Cryptography;
using System.Text;

namespace Pentra.Application.Execution;

/// <summary>Input from the web app to request a new tool run.</summary>
public sealed record RequestRunInput(
    int ProjectId,
    int PhaseId,
    int SecurityToolId,
    int ScopeTargetId,
    IReadOnlyDictionary<string, string> Parameters,
    bool ConfirmedActive);

/// <summary>Limits and caps applied by the execution engine, independent of any tool.</summary>
public static class ExecutionText
{
    /// <summary>Max characters kept for raw stdout/stderr columns.</summary>
    public const int MaxOutputChars = 100_000;

    /// <summary>Max characters kept per streamed log line.</summary>
    public const int MaxLogLineChars = 4_000;

    public static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max
            ? value ?? string.Empty
            : value[..max] + "\n…[truncated]";

    public static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexStringLower(bytes);
    }
}
