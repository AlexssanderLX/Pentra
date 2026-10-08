using System.Xml.Linq;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Enums;

namespace Pentra.Application.Execution.Adapters;

/// <summary>
/// Nmap adapter — controlled service/port discovery with structured XML output.
/// Uses an unprivileged TCP connect scan (-sT) so the container needs no raw-socket
/// capabilities. Active scan: requires confirmation.
/// </summary>
public sealed class NmapAdapter : IToolAdapter
{
    private static readonly string[] AllowedKeys = ["serviceDetection", "topPorts", "timing"];
    private static readonly string[] TimingChoices = ["T2", "T3", "T4"];

    public string Slug => "nmap";

    public ToolDefinition Definition { get; } = new(
        Slug: "nmap",
        DisplayName: "Nmap",
        ImageRef: "instrumentisto/nmap:7.95",
        Risk: ToolRiskLevel.Active,
        Limits: new ExecutionLimits(TimeoutSeconds: 600, MemoryBytes: 512L * 1024 * 1024, Cpus: 1.0, PidsLimit: 256, Network: ContainerNetwork.Bridge),
        Capabilities: "TCP connect scan of top ports with optional service detection; structured XML output.");

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

        var serviceDetection = AdapterInput.TryGetBool(parameters, "serviceDetection", false, out var e1);
        if (e1 is not null) return ArgumentBuildResult.Failure(e1);

        var topPorts = AdapterInput.TryGetIntInRange(parameters, "topPorts", 1, 1000, 100, out var e2);
        if (e2 is not null) return ArgumentBuildResult.Failure(e2);

        var timing = AdapterInput.TryGetChoice(parameters, "timing", TimingChoices, "T3", out var e3);
        if (e3 is not null) return ArgumentBuildResult.Failure(e3);

        var args = new List<string>
        {
            "-sT",            // unprivileged TCP connect scan
            "-Pn",            // skip host discovery (treat host as up)
            $"-{timing}",
            "--top-ports", topPorts!.Value.ToString(),
            "-oX", "-"        // XML to stdout
        };

        if (serviceDetection == true)
        {
            args.Add("-sV");
        }

        args.Add("--");       // end of options — the target can never be a flag
        args.Add(targetValue.Trim());

        return ArgumentBuildResult.Success(args);
    }

    public ToolParseResult Parse(string stdout, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return ToolParseResult.Empty("Open ports", "No output produced.");
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(stdout);
        }
        catch (System.Xml.XmlException)
        {
            return ToolParseResult.Empty("Open ports", "Output was not valid Nmap XML.");
        }

        var rows = new List<IReadOnlyList<string>>();
        foreach (var host in doc.Descendants("host"))
        {
            var address = host.Elements("address").FirstOrDefault()?.Attribute("addr")?.Value ?? "";
            var hostname = host.Element("hostnames")?.Elements("hostname").FirstOrDefault()?.Attribute("name")?.Value;
            var label = string.IsNullOrEmpty(hostname) ? address : $"{hostname} ({address})";

            foreach (var port in host.Element("ports")?.Elements("port") ?? Enumerable.Empty<XElement>())
            {
                var state = port.Element("state")?.Attribute("state")?.Value ?? "";
                if (state != "open")
                {
                    continue;
                }

                var service = port.Element("service");
                var product = service?.Attribute("product")?.Value ?? "";
                var version = service?.Attribute("version")?.Value ?? "";
                rows.Add(new[]
                {
                    label,
                    port.Attribute("portid")?.Value ?? "",
                    port.Attribute("protocol")?.Value ?? "",
                    service?.Attribute("name")?.Value ?? "",
                    string.Join(" ", new[] { product, version }.Where(s => !string.IsNullOrEmpty(s)))
                });
            }
        }

        var summary = rows.Count == 0 ? "No open ports found." : $"{rows.Count} open port(s) found.";
        return new ToolParseResult("Open ports", ["Host", "Port", "Proto", "Service", "Product"], rows, summary);
    }
}
