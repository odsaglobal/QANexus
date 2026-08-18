namespace ATIP.Application.Features.Explorer.Agent;

/// <summary>Shape the LLM returns when repairing a broken element locator.</summary>
public sealed class LocatorHealingResult
{
    /// <summary>Locator strategy: CSS or XPath (the healer verifies these against the live DOM).</summary>
    public string? Strategy { get; set; }

    /// <summary>The corrected selector value.</summary>
    public string? Value { get; set; }

    /// <summary>Model confidence in the healed locator, 0..1.</summary>
    public double Confidence { get; set; }
}
