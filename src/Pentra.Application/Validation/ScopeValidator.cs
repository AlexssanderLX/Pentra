using System.Net;
using System.Text.RegularExpressions;
using Pentra.Domain.Enums;

namespace Pentra.Application.Validation;

/// <summary>
/// Validates scope target values by kind. Input validation is a security
/// requirement: only well-formed, understood values enter the scope so that a
/// future execution gate has a trustworthy foundation.
/// </summary>
public static partial class ScopeValidator
{
    // Conservative hostname/domain check (labels, TLD required for Domain).
    [GeneratedRegex(@"^(?=.{1,253}$)(?!-)([A-Za-z0-9-]{1,63}\.)+[A-Za-z]{2,63}$")]
    private static partial Regex DomainRegex();

    [GeneratedRegex(@"^(?=.{1,253}$)(?!-)[A-Za-z0-9-]{1,63}(\.[A-Za-z0-9-]{1,63})*$")]
    private static partial Regex HostRegex();

    /// <summary>
    /// Returns an error message when <paramref name="value"/> is not valid for
    /// the given <paramref name="kind"/>, or null when it is valid.
    /// </summary>
    public static string? Validate(TargetKind kind, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Target value is required.";
        }

        value = value.Trim();

        return kind switch
        {
            TargetKind.Domain => DomainRegex().IsMatch(value) ? null : "Invalid domain name.",
            TargetKind.Host => HostRegex().IsMatch(value) ? null : "Invalid host name.",
            TargetKind.IpAddress => IPAddress.TryParse(value, out _) ? null : "Invalid IP address.",
            TargetKind.CidrRange => IsValidCidr(value) ? null : "Invalid CIDR range (expected e.g. 10.0.0.0/24).",
            TargetKind.Url => IsValidHttpUrl(value) ? null : "Invalid URL (must be http or https).",
            _ => "Unknown target kind."
        };
    }

    private static bool IsValidCidr(string value)
    {
        var parts = value.Split('/');
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var address))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var prefix) || prefix < 0)
        {
            return false;
        }

        var max = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32;
        return prefix <= max;
    }

    private static bool IsValidHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
