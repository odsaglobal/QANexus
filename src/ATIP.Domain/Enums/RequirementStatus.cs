namespace ATIP.Domain.Enums;

/// <summary>Processing lifecycle of an uploaded requirement document.</summary>
public enum RequirementStatus
{
    /// <summary>Uploaded and text-extracted, awaiting AI analysis.</summary>
    Uploaded = 0,

    /// <summary>AI extraction of modules/features/stories is in progress.</summary>
    Analyzing = 1,

    /// <summary>AI analysis completed successfully.</summary>
    Analyzed = 2,

    /// <summary>Text extraction or AI analysis failed; see the error message.</summary>
    Failed = 3
}
