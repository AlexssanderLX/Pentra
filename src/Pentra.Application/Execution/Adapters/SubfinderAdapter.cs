using System.Text.Json;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Enums;

namespace Pentra.Application.Execution.Adapters;

/// <summary>
/// Subfinder adapter — passive subdomain enumeration (JSON lines output).
/// Passive: it discovers names but never connects to them, so discovery stays
/// in-scope by construction (no active probing of discovered hosts here).
/// </summary>
public sealed class SubfinderAdapter : IToolAdapter
{
    private static readonly string[] AllowedKeys = ["allSources"];

    public string Slug => "subfinder";

    public ToolDefinition Definition { get; } = new(
        Slug: "subfinder",
        DisplayName: "Subfinder",
        ImageRef: "projectdiscovery/subfinder:v2.6.6",
        Risk: ToolRiskLevel.Passive,
        Limits: new ExecutionLimits(TimeoutSeconds: 300, MemoryBytes: 256L * 1024 * 1024, Cpus: 1.0, PidsLimit: 128, Network: ContainerNetwork.Bridge),
        Capabilities: "Passive subdomain enumeration from public sources; JSONL output.");

    public ArgumentBuildResult BuildArguments(string targetValue, IReadOnlyDictionary<string, string> parameters)
    {
        var unknown = AdapterInput.UnknownKeys(parameters, AllowedKeys);
        if (unknown.Count > 0)
        {
            return ArgumentBuildResult.Failure($"Unknown parameter(s): {string.Join(", ", unknown)}.");
        }

        var targetError = AdapterInput.ValidateTarget(targetValue);
        if (targetError is not null)
        {
            return ArgumentBuildResult.Failure(targetError);
        }

        // Subfinder enumerates a domain; a scheme/path makes no sense here.
        var domain = targetValue.Trim();
        if (domain.Contains('/') || domain.Contains(':'))
        {
            return ArgumentBuildResult.Failure("Subfinder expects a bare domain (no scheme, port or path).");
        }

        var allSources = AdapterInput.TryGetBool(parameters, "allSources", false, out var e1);
        if (e1 is not null) return ArgumentBuildResult.Failure(e1);

        var args = new List<string> { "-silent", "-oJ", "-d", domain };
        if (allSources == true)
        {
            args.Add("-all");
        }

        return ArgumentBuildResult.Success(args);
    }

    public ToolParseResult Parse(string stdout, string stderr)
    {
        var rows = new List<IReadOnlyList<string>>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in SplitLines(stdout))
        {
            string host;
            string source = "";
            var trimmed = line.Trim();
            if (trimmed.StartsWith('{'))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    var root = doc.RootElement;
                    host = root.TryGetProperty("host", out var h) ? h.GetString() ?? "" : "";
                    source = root.TryGetProperty("source", out var s) ? s.GetString() ?? "" : "";
                }
                catch (JsonException)
                {
                    continue;
                }
            }
            else
            {
                host = trimmed; // plain host-per-line fallback
            }

            if (!string.IsNullOrEmpty(host) && seen.Add(host))
            {
                rows.Add(new[] { host, source });
            }
        }

        var summary = rows.Count == 0 ? "No subdomains discovered." : $"{rows.Count} subdomain(s) discovered.";
        return new ToolParseResult("Subdomains", ["Subdomain", "Source"], rows, summary);
    }

    private static IEnumerable<string> SplitLines(string text) =>
        string.IsNullOrEmpty(text)
            ? Enumerable.Empty<string>()
            : text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
