namespace ATIP.Application.Common.Interfaces;

/// <summary>Abstracts the system clock so time-dependent logic can be unit-tested deterministically.</summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
