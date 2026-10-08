namespace Pentra.Domain.Enums;

/// <summary>Network mode granted to a tool container.</summary>
public enum ContainerNetwork
{
    /// <summary>No network at all (for tools that need none).</summary>
    None = 0,

    /// <summary>Default bridge network (outbound to the target).</summary>
    Bridge = 1
}
