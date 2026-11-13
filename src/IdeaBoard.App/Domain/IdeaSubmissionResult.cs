namespace IdeaBoard.App.Domain;

/// <summary>
/// Outcome of validating a submission: the trimmed values that would be stored, and the
/// problems found, keyed by field so that each message can be tied to its own input.
/// </summary>
public class IdeaSubmissionResult
{
    #region Construction

    private readonly Dictionary<string, string> errors;

    /// <summary>Creates a result from the trimmed values and the problems found.</summary>
    public IdeaSubmissionResult(string title, string description, IDictionary<string, string> errors)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(errors);

        Title = title;
        Description = description;
        this.errors = new Dictionary<string, string>(errors, StringComparer.Ordinal);
    }

    #endregion


    #region State

    /// <summary>The title as it would be stored, already trimmed.</summary>
    public string Title { get; }

    /// <summary>The description as it would be stored, already trimmed.</summary>
    public string Description { get; }

    /// <summary>Problems found, keyed by field name.</summary>
    public IReadOnlyDictionary<string, string> Errors
    {
        get
        {
            return errors;
        }
    }

    /// <summary>True when the submission may be stored.</summary>
    public bool IsValid
    {
        get
        {
            return errors.Count == 0;
        }
    }

    #endregion
}
