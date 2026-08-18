using ATIP.Application.Common.Interfaces;

namespace ATIP.Infrastructure.Common;

/// <summary>Production clock backed by the OS. Swapped for a fake in tests.</summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
