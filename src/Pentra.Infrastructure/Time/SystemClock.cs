using Pentra.Domain.Abstractions;

namespace Pentra.Infrastructure.Time;

public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
