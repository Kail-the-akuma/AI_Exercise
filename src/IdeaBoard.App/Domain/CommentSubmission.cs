namespace IdeaBoard.App.Domain;

/// <summary>
/// The rules a comment has to satisfy, with no dependency on the web layer or on the
/// database. Server-side validation is the one that counts: the browser check is a
/// convenience, and a submission that bypasses it has to be rejected just the same.
/// </summary>
public static class CommentSubmission
{
    #region Messages

    /// <summary>Shown when the text is empty or made only of whitespace.</summary>
    public const string MissingTextMessage = "Escreva um comentário.";

    /// <summary>Shown when the trimmed text is longer than the limit.</summary>
    public const string TextTooLongMessage = "O comentário não pode ultrapassar 2000 caracteres.";

    #endregion


    #region Validation

    /// <summary>
    /// Trims leading and trailing whitespace, then validates. Trimming happens before the
    /// check, which is what makes a comment of nothing but spaces an empty comment.
    /// </summary>
    public static CommentSubmissionResult Validate(string? text)
    {
        string trimmed = Trim(text);

        Dictionary<string, string> errors = new Dictionary<string, string>(StringComparer.Ordinal);

        ValidateText(trimmed, errors);

        return new CommentSubmissionResult(trimmed, errors);
    }


    /// <summary>Records the first problem found, so the field shows one message.</summary>
    private static void ValidateText(string text, Dictionary<string, string> errors)
    {
        if (text.Length == 0)
        {
            errors.Add(CommentLimits.TextField, MissingTextMessage);
            return;
        }

        if (text.Length > CommentLimits.TextMaximumLength)
        {
            errors.Add(CommentLimits.TextField, TextTooLongMessage);
        }
    }


    /// <summary>Trims a value that may be absent.</summary>
    private static string Trim(string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return value.Trim();
    }

    #endregion
}
