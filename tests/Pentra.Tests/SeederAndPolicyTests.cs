using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pentra.Application.Security;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Entities;
using Pentra.Infrastructure.Persistence;
using Pentra.Tests.TestSupport;
using Xunit;

namespace Pentra.Tests;

public class SeederAndPolicyTests
{
    [Fact]
    public async Task Seeder_CreatesSevenPhasesAndEightTools()
    {
        using var h = new TestHarness();

        await DataSeeder.SeedAsync(h.Db, h.Clock);

        (await h.Db.Phases.CountAsync()).Should().Be(7);
        (await h.Db.Tools.CountAsync()).Should().Be(8);

        var orders = await h.Db.Phases.OrderBy(p => p.Order).Select(p => p.Order).ToListAsync();
        orders.Should().Equal(1, 2, 3, 4, 5, 6, 7);

        var slugs = await h.Db.Tools.Select(t => t.Slug).ToListAsync();
        slugs.Should().Contain(new[] { "nmap", "subfinder", "httpx", "gobuster", "ffuf", "nikto", "nuclei", "owasp-zap" });
    }

    [Fact]
    public async Task Seeder_IsIdempotent()
    {
        using var h = new TestHarness();

        await DataSeeder.SeedAsync(h.Db, h.Clock);
        await DataSeeder.SeedAsync(h.Db, h.Clock);

        (await h.Db.Phases.CountAsync()).Should().Be(7);
        (await h.Db.Tools.CountAsync()).Should().Be(8);
    }

    [Fact]
    public async Task Seeder_EveryToolMapsToAnExistingPhase()
    {
        using var h = new TestHarness();
        await DataSeeder.SeedAsync(h.Db, h.Clock);

        var phaseIds = await h.Db.Phases.Select(p => p.Id).ToListAsync();
        var toolPhaseIds = await h.Db.Tools.Select(t => t.DefaultPhaseId).ToListAsync();

        toolPhaseIds.Should().OnlyContain(id => phaseIds.Contains(id));
    }

    [Fact]
    public void DenyAllPolicy_DeniesEveryRequest()
    {
        var policy = new DenyAllToolExecutionPolicy();
        var request = new ToolExecutionRequest(
            1, 1,
            new SecurityTool { Slug = "nmap", Name = "Nmap" },
            new ScopeTarget { Value = "example.com", IsAuthorized = true },
            Array.Empty<string>());

        var decision = policy.Authorize(request);

        decision.IsAllowed.Should().BeFalse();
        decision.Reason.Should().NotBeNullOrWhiteSpace();
    }
}
