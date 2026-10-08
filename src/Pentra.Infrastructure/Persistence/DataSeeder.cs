using Microsoft.EntityFrameworkCore;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Entities;
using Pentra.Domain.Enums;

namespace Pentra.Infrastructure.Persistence;

/// <summary>
/// Seeds reference data: the seven standard pentest phases and the initial
/// tool catalog. Idempotent — safe to run on every startup.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(PentraDbContext db, ISystemClock clock, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        await SeedPhasesAsync(db, now, ct);
        await SeedToolsAsync(db, now, ct);
    }

    private static async Task SeedPhasesAsync(PentraDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.Phases.AnyAsync(ct))
        {
            return;
        }

        var phases = new[]
        {
            new PentestPhase { Order = 1, Slug = "reconnaissance", Name = "Reconnaissance", Description = "Passive and active information gathering about the target and its attack surface.", CreatedAt = now },
            new PentestPhase { Order = 2, Slug = "scanning-enumeration", Name = "Scanning & Enumeration", Description = "Discover live hosts, open ports, services and exposed content.", CreatedAt = now },
            new PentestPhase { Order = 3, Slug = "vulnerability-analysis", Name = "Vulnerability Analysis", Description = "Identify and validate potential vulnerabilities across the scope.", CreatedAt = now },
            new PentestPhase { Order = 4, Slug = "exploitation", Name = "Exploitation", Description = "Safely attempt to exploit confirmed weaknesses within authorized scope.", CreatedAt = now },
            new PentestPhase { Order = 5, Slug = "post-exploitation", Name = "Post-Exploitation", Description = "Assess impact, pivoting potential and persistence within rules of engagement.", CreatedAt = now },
            new PentestPhase { Order = 6, Slug = "reporting", Name = "Reporting", Description = "Document findings, evidence and risk ratings for stakeholders.", CreatedAt = now },
            new PentestPhase { Order = 7, Slug = "remediation-retest", Name = "Remediation & Retest", Description = "Track remediation and retest to confirm issues are resolved.", CreatedAt = now }
        };

        db.Phases.AddRange(phases);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedToolsAsync(PentraDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.Tools.AnyAsync(ct))
        {
            return;
        }

        var phaseIdBySlug = await db.Phases.ToDictionaryAsync(p => p.Slug, p => p.Id, ct);

        int Phase(string slug) => phaseIdBySlug[slug];

        var tools = new[]
        {
            new SecurityTool { Slug = "nmap", Name = "Nmap", Category = ToolCategory.Scanning, Command = "nmap",
                Description = "Network discovery and port/service scanner.", ReferenceUrl = "https://nmap.org/",
                DefaultPhaseId = Phase("scanning-enumeration"), CreatedAt = now },
            new SecurityTool { Slug = "subfinder", Name = "Subfinder", Category = ToolCategory.Reconnaissance, Command = "subfinder",
                Description = "Passive subdomain discovery tool.", ReferenceUrl = "https://github.com/projectdiscovery/subfinder",
                DefaultPhaseId = Phase("reconnaissance"), CreatedAt = now },
            new SecurityTool { Slug = "httpx", Name = "httpx", Category = ToolCategory.Reconnaissance, Command = "httpx",
                Description = "Fast HTTP probing and host fingerprinting toolkit.", ReferenceUrl = "https://github.com/projectdiscovery/httpx",
                DefaultPhaseId = Phase("reconnaissance"), CreatedAt = now },
            new SecurityTool { Slug = "gobuster", Name = "Gobuster", Category = ToolCategory.ContentDiscovery, Command = "gobuster",
                Description = "Directory, DNS and vhost brute-forcing tool.", ReferenceUrl = "https://github.com/OJ/gobuster",
                DefaultPhaseId = Phase("scanning-enumeration"), CreatedAt = now },
            new SecurityTool { Slug = "ffuf", Name = "ffuf", Category = ToolCategory.Fuzzing, Command = "ffuf",
                Description = "Fast web fuzzer for content and parameter discovery.", ReferenceUrl = "https://github.com/ffuf/ffuf",
                DefaultPhaseId = Phase("scanning-enumeration"), CreatedAt = now },
            new SecurityTool { Slug = "nikto", Name = "Nikto", Category = ToolCategory.WebScanner, Command = "nikto",
                Description = "Web server scanner for known issues and misconfigurations.", ReferenceUrl = "https://github.com/sullo/nikto",
                DefaultPhaseId = Phase("vulnerability-analysis"), CreatedAt = now },
            new SecurityTool { Slug = "nuclei", Name = "Nuclei", Category = ToolCategory.VulnerabilityScanner, Command = "nuclei",
                Description = "Template-based vulnerability scanner.", ReferenceUrl = "https://github.com/projectdiscovery/nuclei",
                DefaultPhaseId = Phase("vulnerability-analysis"), CreatedAt = now },
            new SecurityTool { Slug = "owasp-zap", Name = "OWASP ZAP", Category = ToolCategory.Proxy, Command = "zap.sh",
                Description = "Web application security scanner and intercepting proxy.", ReferenceUrl = "https://www.zaproxy.org/",
                DefaultPhaseId = Phase("vulnerability-analysis"), CreatedAt = now }
        };

        db.Tools.AddRange(tools);
        await db.SaveChangesAsync(ct);
    }
}
