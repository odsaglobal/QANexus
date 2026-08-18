namespace ATIP.Domain.Enums;

/// <summary>Classifies the purpose of a project <see cref="Entities.Environment"/>.</summary>
public enum EnvironmentType
{
    Development = 0,
    Test = 1,
    Staging = 2,
    Production = 3,
    Sandbox = 4
}
