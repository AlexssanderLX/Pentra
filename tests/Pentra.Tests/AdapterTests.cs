using FluentAssertions;
using Pentra.Application.Execution.Adapters;
using Xunit;

namespace Pentra.Tests;

public class AdapterTests
{
    private static Dictionary<string, string> NoParams() => new();

    // --- Nmap ---------------------------------------------------------------

    [Fact]
    public void Nmap_BuildArguments_ProducesSafeConnectScanWithTargetAfterSeparator()
    {
        var adapter = new NmapAdapter();

        var result = adapter.BuildArguments("scanme.example.com", NoParams());

        result.Ok.Should().BeTrue();
        result.Arguments.Should().Contain("-sT");
        result.Arguments.Should().ContainInOrder("-oX", "-");
        // target must come right after the "--" end-of-options separator
        result.Arguments.Should().ContainInOrder("--", "scanme.example.com");
    }

    [Fact]
    public void Nmap_BuildArguments_RejectsUnknownParameter()
    {
        var adapter = new NmapAdapter();
        var result = adapter.BuildArguments("example.com", new Dictionary<string, string> { ["scriptArgs"] = "x" });
        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("Unknown");
    }

    [Theory]
    [InlineData("-oN /tmp/evil")]      // argument injection (leading dash)
    [InlineData("; rm -rf /")]          // shell metacharacters
    [InlineData("a b")]                 // whitespace
    [InlineData("--script=http-vuln")] // flag masquerading as target
    public void Nmap_BuildArguments_RejectsHostileTargets(string target)
    {
        var adapter = new NmapAdapter();
        var result = adapter.BuildArguments(target, NoParams());
        result.Ok.Should().BeFalse();
    }

    [Fact]
    public void Nmap_BuildArguments_RejectsOutOfRangeTopPorts()
    {
        var adapter = new NmapAdapter();
        var result = adapter.BuildArguments("example.com", new Dictionary<string, string> { ["topPorts"] = "99999" });
        result.Ok.Should().BeFalse();
    }

    [Fact]
    public void Nmap_Parse_ExtractsOpenPorts()
    {
        const string xml = """
        <?xml version="1.0"?>
        <nmaprun>
          <host>
            <address addr="93.184.216.34" addrtype="ipv4"/>
            <hostnames><hostname name="example.com"/></hostnames>
            <ports>
              <port protocol="tcp" portid="80">
                <state state="open"/>
                <service name="http" product="nginx" version="1.25"/>
              </port>
              <port protocol="tcp" portid="81">
                <state state="closed"/>
                <service name="hosts2-ns"/>
              </port>
            </ports>
          </host>
        </nmaprun>
        """;

        var result = new NmapAdapter().Parse(xml, "");

        result.Rows.Should().HaveCount(1); // only the open port
        result.Rows[0].Should().ContainInOrder("example.com (93.184.216.34)", "80", "tcp", "http");
        result.Rows[0][4].Should().Contain("nginx");
    }

    [Fact]
    public void Nmap_Parse_HandlesInvalidXmlGracefully()
    {
        var result = new NmapAdapter().Parse("not xml at all", "");
        result.Rows.Should().BeEmpty();
        result.Summary.Should().NotBeNullOrWhiteSpace();
    }

    // --- Subfinder ----------------------------------------------------------

    [Fact]
    public void Subfinder_BuildArguments_AcceptsBareDomain()
    {
        var result = new SubfinderAdapter().BuildArguments("example.com", NoParams());
        result.Ok.Should().BeTrue();
        result.Arguments.Should().ContainInOrder("-d", "example.com");
        result.Arguments.Should().Contain("-oJ");
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("example.com/path")]
    [InlineData("example.com:443")]
    public void Subfinder_BuildArguments_RejectsNonDomainTargets(string target)
    {
        var result = new SubfinderAdapter().BuildArguments(target, NoParams());
        result.Ok.Should().BeFalse();
    }

    [Fact]
    public void Subfinder_Parse_ReadsJsonlAndDeduplicates()
    {
        const string jsonl = """
        {"host":"a.example.com","source":"crtsh"}
        {"host":"b.example.com","source":"wayback"}
        {"host":"a.example.com","source":"dnsdumpster"}
        """;

        var result = new SubfinderAdapter().Parse(jsonl, "");

        result.Rows.Should().HaveCount(2);
        result.Rows.Select(r => r[0]).Should().Contain(new[] { "a.example.com", "b.example.com" });
    }

    [Fact]
    public void Subfinder_Parse_FallsBackToPlainLines()
    {
        var result = new SubfinderAdapter().Parse("a.example.com\nb.example.com\n", "");
        result.Rows.Should().HaveCount(2);
    }

    // --- httpx --------------------------------------------------------------

    [Fact]
    public void Httpx_BuildArguments_DoesNotFollowRedirectsByDefault()
    {
        var result = new HttpxAdapter().BuildArguments("https://example.com", NoParams());
        result.Ok.Should().BeTrue();
        result.Arguments.Should().ContainInOrder("-u", "https://example.com");
        result.Arguments.Should().NotContain("-follow-redirects");
    }

    [Fact]
    public void Httpx_BuildArguments_AddsRedirectsWhenRequested()
    {
        var result = new HttpxAdapter().BuildArguments("https://example.com", new Dictionary<string, string> { ["followRedirects"] = "true" });
        result.Ok.Should().BeTrue();
        result.Arguments.Should().Contain("-follow-redirects");
    }

    [Fact]
    public void Httpx_Parse_ReadsJsonl()
    {
        const string jsonl = """
        {"url":"https://example.com","status_code":200,"title":"Example","tech":["nginx","PHP"]}
        {"url":"https://old.example.com","status_code":301,"title":""}
        """;

        var result = new HttpxAdapter().Parse(jsonl, "");

        result.Rows.Should().HaveCount(2);
        result.Rows[0].Should().ContainInOrder("https://example.com", "200", "Example");
        result.Rows[0][3].Should().Contain("nginx");
    }
}
