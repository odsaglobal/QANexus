namespace ATIP.Domain.Enums;

/// <summary>Lifecycle of a browser-based application exploration session.</summary>
public enum ExplorationStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,

    /// <summary>
    /// The agent hit a genuine ambiguity it must not guess its way through, and has put a question to
    /// the user. The browser is held open on the page that raised the question, so answering resumes
    /// the same exploration rather than restarting it.
    /// </summary>
    AwaitingInput = 5
}
