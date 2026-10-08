using System.Text.Json;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Enums;

namespace Pentra.Application.Execution.Adapters;

/// <summary>
/// httpx (ProjectDiscovery) adapter — HTTP service probing with status, title and
/// technology detection (JSONL output). Redirects are OFF by default so the probe
/// never follows a redirect to an out-of-scope host.
/// </summary>
public sealed class HttpxAdapter : IToolAdapter
{
    private static readonly string[] AllowedKeys = ["followRedirects", "techDetect"];

    public string Slug => "httpx";

    public ToolDefinition Definition { get; } = new(
        Slug: "httpx",
        DisplayName: "httpx",
        ImageRef: "projectdiscovery/httpx:v1.6.9",
        Risk: ToolRiskLevel.Active,
        Limits: new ExecutionLimits(TimeoutSeconds: 300, MemoryBytes: 256L * 1024 * 1024, Cpus: 1.0, PidsLimit: 128, Network: ContainerNetwork.Bridge),
        Capabilities: "HTTP probing: status code, page title and technology detection; JSONL output.");

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

        var followRedirects = AdapterInput.TryGetBool(parameters, "followRedirects", false, out var e1);
        if (e1 is not null) return ArgumentBuildResult.Failure(e1);

        var techDetect = AdapterInput.TryGetBool(parameters, "techDetect", true, out var e2);
        if (e2 is not null) return ArgumentBuildResult.Failure(e2);

        var args = new List<string>
        {
            "-silent",
            "-json",
            "-status-code",
            "-title",
            "-u", targetValue.Trim()   // -u takes the target as a value
        };

        if (techDetect == true)
        {
            args.Add("-tech-detect");
        }

        if (followRedirects == true)
        {
            args.Add("-follow-redirects");
        }

        return ArgumentBuildResult.Success(args);
    }

    public ToolParseResult Parse(string stdout, string stderr)
    {
        var rows = new List<IReadOnlyList<string>>();

        foreach (var line in (stdout ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith('{'))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var url = GetString(root, "url");
                var status = root.TryGetProperty("status_code", out var sc) && sc.ValueKind == JsonValueKind.Number
                    ? sc.GetInt32().ToString()
                    : GetString(root, "status_code");
                var title = GetString(root, "title");
                var tech = "";
                if (root.TryGetProperty("tech", out var t) && t.ValueKind == JsonValueKind.Array)
                {
                    tech = string.Join(", ", t.EnumerateArray().Select(x => x.GetString()).Where(s => !string.IsNullOrEmpty(s)));
                }

                rows.Add(new[] { url, status, title, tech });
            }
            catch (JsonException)
            {
                // ignore malformed line
            }
        }

        var summary = rows.Count == 0 ? "No live HTTP services found." : $"{rows.Count} HTTP response(s).";
        return new ToolParseResult("HTTP services", ["URL", "Status", "Title", "Tech"], rows, summary);
    }

    private static string GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
