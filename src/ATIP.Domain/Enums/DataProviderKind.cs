namespace ATIP.Domain.Enums;

/// <summary>Relational engine behind a <see cref="Entities.DataConnection"/>.</summary>
public enum DataProviderKind
{
    PostgreSql = 0,
    SqlServer = 1,
    MySql = 2
}
