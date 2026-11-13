namespace IdeaBoard.App.Configuration;

/// <summary>
/// Outcome of the startup configuration check: either nothing to report, or the complete
/// set of problems, so that someone starting the application for the first time fixes
/// every one of them in a single pass instead of discovering them one restart at a time.
/// </summary>
public class ConfigurationValidationResult
{
    #region Construction

    private readonly List<string> problems;

    /// <summary>Creates a result carrying the problems found, which may be none.</summary>
    public ConfigurationValidationResult(IEnumerable<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);

        this.problems = new List<string>(problems);
    }

    #endregion


    #region State

    /// <summary>Every problem found, in the order they were checked.</summary>
    public IReadOnlyList<string> Problems
    {
        get
        {
            return problems;
        }
    }

    /// <summary>True when the application may start.</summary>
    public bool IsValid
    {
        get
        {
            return problems.Count == 0;
        }
    }

    #endregion


    #region Reporting

    /// <summary>
    /// Builds the message shown when startup is refused. It names each missing key, because
    /// the person reading it has just cloned the repository and has no other context.
    /// </summary>
    public string BuildFailureMessage()
    {
        System.Text.StringBuilder message = new System.Text.StringBuilder();

        message.AppendLine("IdeaBoard refused to start because its configuration is incomplete.");
        message.AppendLine();

        foreach (string problem in problems)
        {
            message.Append("  - ");
            message.AppendLine(problem);
        }

        message.AppendLine();
        message.AppendLine("Set these values in user secrets, in an untracked appsettings file, or in the");
        message.AppendLine("server configuration. They are deliberately absent from the tracked settings.");

        return message.ToString();
    }

    #endregion
}
