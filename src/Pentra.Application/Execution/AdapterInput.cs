using System.Text.RegularExpressions;

namespace Pentra.Application.Execution;

/// <summary>
/// Validation helpers shared by tool adapters. Because arguments are passed as a
/// vector (never a shell string), classic shell injection is impossible; the real
/// risk is <b>argument injection</b> (a hostile value that the tool treats as a
/// flag). These helpers reject such values and constrain every parameter.
/// </summary>
public static partial class AdapterInput
{
    // Target: hostnames, IPs, CIDRs and simple URLs. No whitespace, no control
    // chars, must not start with '-', limited punctuation. URLs with query
    // strings are rejected on purpose.
    [GeneratedRegex(@"^(?!-)[A-Za-z0-9._:/\-]{1,255}$")]
    private static partial Regex SafeTargetRegex();

    /// <summary>Returns an error message if the target is unsafe, else null.</summary>
    public static string? ValidateTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return "Target is required.";
        }

        target = target.Trim();

        // Allow a scheme prefix for URLs, validating the remainder.
        var toCheck = target;
        if (toCheck.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            toCheck = toCheck["http://".Length..];
        }
        else if (toCheck.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            toCheck = toCheck["https://".Length..];
        }

        if (target.StartsWith('-') || !SafeTargetRegex().IsMatch(toCheck))
        {
            return "Target contains characters that are not allowed.";
        }

        return null;
    }

    /// <summary>Reads a boolean parameter (default when absent), rejecting junk values.</summary>
    public static bool? TryGetBool(IReadOnlyDictionary<string, string> p, string key, bool defaultValue, out string? error)
    {
        error = null;
        if (!p.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        raw = raw.Trim().ToLowerInvariant();
        if (raw is "true" or "on" or "1" or "yes") return true;
        if (raw is "false" or "off" or "0" or "no") return false;

        error = $"Parameter '{key}' must be a boolean.";
        return null;
    }

    /// <summary>Reads an integer parameter within [min,max] (default when absent).</summary>
    public static int? TryGetIntInRange(IReadOnlyDictionary<string, string> p, string key, int min, int max, int defaultValue, out string? error)
    {
        error = null;
        if (!p.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!int.TryParse(raw.Trim(), out var value))
        {
            error = $"Parameter '{key}' must be an integer.";
            return null;
        }

        if (value < min || value > max)
        {
            error = $"Parameter '{key}' must be between {min} and {max}.";
            return null;
        }

        return value;
    }

    /// <summary>Reads a value constrained to an allow-list (default when absent).</summary>
    public static string? TryGetChoice(IReadOnlyDictionary<string, string> p, string key, IReadOnlyCollection<string> allowed, string defaultValue, out string? error)
    {
        error = null;
        if (!p.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        raw = raw.Trim();
        if (!allowed.Contains(raw))
        {
            error = $"Parameter '{key}' must be one of: {string.Join(", ", allowed)}.";
            return null;
        }

        return raw;
    }

    /// <summary>Returns the keys in <paramref name="p"/> that are not in the allow-list.</summary>
    public static IReadOnlyList<string> UnknownKeys(IReadOnlyDictionary<string, string> p, IReadOnlyCollection<string> allowed) =>
        p.Keys.Where(k => !allowed.Contains(k)).ToList();
}
