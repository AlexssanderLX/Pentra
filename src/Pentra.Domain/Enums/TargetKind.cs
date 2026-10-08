namespace Pentra.Domain.Enums;

/// <summary>
/// Classification of an authorized scope target.
/// </summary>
public enum TargetKind
{
    Domain = 0,
    IpAddress = 1,
    CidrRange = 2,
    Url = 3,
    Host = 4
}
