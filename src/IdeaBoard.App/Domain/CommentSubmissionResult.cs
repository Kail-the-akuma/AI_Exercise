namespace IdeaBoard.App.Domain;

/// <summary>
/// Outcome of validating a comment: the trimmed text that would be stored, and the problems
/// found, keyed by field so that the message can be tied to its own input.
///
/// It is the same shape as <see cref="IdeaSubmissionResult"/> because the page that renders
/// it does the same thing with it. Merging the two into one generic carrier would make the
/// two forms share a type that neither of them is, for the sake of removing four lines.
/// </summary>
public class CommentSubmissionResult
{
    #region Construction

    private readonly Dictionary<string, string> errors;

    /// <summary>Creates a result from the trimmed text and the problems found.</summary>
    public CommentSubmissionResult(string text, IDictionary<string, string> errors)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(errors);

        Text = text;
        this.errors = new Dictionary<string, string>(errors, StringComparer.Ordinal);
    }

    #endregion


    #region State

    /// <summary>The text as it would be stored, already trimmed.</summary>
    public string Text { get; }

    /// <summary>Problems found, keyed by field name.</summary>
    public IReadOnlyDictionary<string, string> Errors
    {
        get
        {
            return errors;
        }
    }

    /// <summary>True when the comment may be stored.</summary>
    public bool IsValid
    {
        get
        {
            return errors.Count == 0;
        }
    }

    #endregion
}
