using ATIP.Application.Common.Models;

namespace ATIP.Application.Common.Interfaces;

public interface ITestRailClient
{
    Task<IReadOnlyList<ImportedTestScenario>> GetCasesAsync(
        int testRailProjectId,
        int? suiteId,
        int? sectionId,
        CancellationToken cancellationToken = default);
}
