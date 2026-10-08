using FluentAssertions;
using Pentra.Application.Validation;
using Pentra.Domain.Enums;
using Xunit;

namespace Pentra.Tests;

public class ScopeValidatorTests
{
    [Theory]
    [InlineData("example.com")]
    [InlineData("sub.example.co.uk")]
    public void Validate_AcceptsValidDomains(string value) =>
        ScopeValidator.Validate(TargetKind.Domain, value).Should().BeNull();

    [Theory]
    [InlineData("not a domain")]
    [InlineData("example")]
    [InlineData("")]
    public void Validate_RejectsInvalidDomains(string value) =>
        ScopeValidator.Validate(TargetKind.Domain, value).Should().NotBeNull();

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("192.168.1.254")]
    [InlineData("::1")]
    public void Validate_AcceptsValidIps(string value) =>
        ScopeValidator.Validate(TargetKind.IpAddress, value).Should().BeNull();

    [Theory]
    [InlineData("10.0.0.0/24")]
    [InlineData("192.168.0.0/16")]
    public void Validate_AcceptsValidCidr(string value) =>
        ScopeValidator.Validate(TargetKind.CidrRange, value).Should().BeNull();

    [Theory]
    [InlineData("10.0.0.0/33")]
    [InlineData("10.0.0.0")]
    [InlineData("10.0.0.0/-1")]
    public void Validate_RejectsInvalidCidr(string value) =>
        ScopeValidator.Validate(TargetKind.CidrRange, value).Should().NotBeNull();

    [Theory]
    [InlineData("https://app.example.com/login")]
    [InlineData("http://example.com")]
    public void Validate_AcceptsValidUrls(string value) =>
        ScopeValidator.Validate(TargetKind.Url, value).Should().BeNull();

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("example.com")]
    public void Validate_RejectsNonHttpUrls(string value) =>
        ScopeValidator.Validate(TargetKind.Url, value).Should().NotBeNull();
}
